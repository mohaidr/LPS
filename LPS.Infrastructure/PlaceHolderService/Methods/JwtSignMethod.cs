using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using LPS.Domain.Common;
using LPS.Domain.Common.Interfaces;
using LPS.Infrastructure.VariableServices.GlobalVariableManager;

namespace LPS.Infrastructure.PlaceHolderService.Methods
{
    /// <summary>
    /// $jwtsign(claims={...}, key=..., algorithm=HS256, expiresIn=3600, variable=name)
    /// Builds and signs a compact JWT (header.payload.signature). The write-side counterpart of
    /// $jwtclaim. Symmetric HS256/HS384/HS512 are supported; RS256/ES256 are not yet implemented.
    /// Automatically adds 'iat' (issued-at) and, when expiresIn &gt; 0, 'exp' — without overriding
    /// any claim already present in 'claims'.
    /// </summary>
    public sealed class JwtSignMethod : MethodBase
    {
        public JwtSignMethod(ParameterExtractorService p, ILogger l, IRuntimeOperationIdProvider op, IVariableManager v, Lazy<IPlaceholderResolverService> r)
            : base(p, l, op, v, r)
        {
        }

        public override string Name => "jwtsign";

        public override async Task<string> ExecuteAsync(string parameters, string sessionId, CancellationToken token)
        {
            string variableName = string.Empty;

            try
            {
                var claimsRaw = await _params.ExtractStringAsync(parameters, "claims", string.Empty, sessionId, token);
                var key = await _params.ExtractStringAsync(parameters, "key", string.Empty, sessionId, token) ?? string.Empty;
                var algorithm = (await _params.ExtractStringAsync(parameters, "algorithm", "HS256", sessionId, token)).ToUpperInvariant();
                var expiresIn = await _params.ExtractNumberAsync(parameters, "expiresIn", 0, sessionId, token);
                variableName = await _params.ExtractStringAsync(parameters, "variable", string.Empty, sessionId, token);

                if (string.IsNullOrWhiteSpace(claimsRaw))
                {
                    await _logger.LogAsync(_op.OperationId, "jwtsign failed. 'claims' (a JSON object) is required.", LPSLoggingLevel.Error, token);
                    await StoreStringVariableAsync(variableName, string.Empty, token);
                    return string.Empty;
                }

                if (string.IsNullOrEmpty(key))
                {
                    await _logger.LogAsync(_op.OperationId, "jwtsign failed. A 'key' is required.", LPSLoggingLevel.Error, token);
                    await StoreStringVariableAsync(variableName, string.Empty, token);
                    return string.Empty;
                }

                using HMAC signer = algorithm switch
                {
                    "HS256" => new HMACSHA256(Encoding.UTF8.GetBytes(key)),
                    "HS384" => new HMACSHA384(Encoding.UTF8.GetBytes(key)),
                    "HS512" => new HMACSHA512(Encoding.UTF8.GetBytes(key)),
                    _ => null
                };

                if (signer == null)
                {
                    await _logger.LogAsync(_op.OperationId, $"jwtsign failed. Algorithm '{algorithm}' is not supported yet. Use HS256, HS384, or HS512.", LPSLoggingLevel.Error, token);
                    await StoreStringVariableAsync(variableName, string.Empty, token);
                    return string.Empty;
                }

                JObject claims;
                try
                {
                    claims = JObject.Parse(claimsRaw);
                }
                catch (Exception ex)
                {
                    await _logger.LogAsync(_op.OperationId, $"jwtsign failed. 'claims' is not valid JSON: {ex.Message}", LPSLoggingLevel.Error, token);
                    await StoreStringVariableAsync(variableName, string.Empty, token);
                    return string.Empty;
                }

                long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                if (claims["iat"] == null) claims["iat"] = now;
                if (expiresIn > 0 && claims["exp"] == null) claims["exp"] = now + expiresIn;

                var header = new JObject { ["alg"] = algorithm, ["typ"] = "JWT" };

                string headerPart = Base64UrlEncode(Encoding.UTF8.GetBytes(header.ToString(Formatting.None)));
                string payloadPart = Base64UrlEncode(Encoding.UTF8.GetBytes(claims.ToString(Formatting.None)));
                string signingInput = headerPart + "." + payloadPart;

                byte[] signature = signer.ComputeHash(Encoding.UTF8.GetBytes(signingInput));
                string jwt = signingInput + "." + Base64UrlEncode(signature);

                await StoreStringVariableAsync(variableName, jwt, token);
                return jwt;
            }
            catch (Exception ex)
            {
                await _logger.LogAsync(_op.OperationId, $"jwtsign failed. {ex}", LPSLoggingLevel.Error, token);
                await StoreStringVariableAsync(variableName, string.Empty, token);
                return string.Empty;
            }
        }

        private static string Base64UrlEncode(byte[] input) =>
            Convert.ToBase64String(input).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }
}
