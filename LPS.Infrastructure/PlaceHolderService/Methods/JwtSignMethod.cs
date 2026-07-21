using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using LPS.Domain.Common;
using LPS.Domain.Common.Interfaces;
using LPS.Infrastructure.LPSClients.SessionManager;
using LPS.Infrastructure.VariableServices.GlobalVariableManager;

namespace LPS.Infrastructure.PlaceHolderService.Methods
{
    /// <summary>
    /// $jwtsign(claims={...}, key=..., algorithm=HS256, expiresIn=3600, variable=name)
    /// Builds and signs a compact JWT (header.payload.signature). The write-side counterpart of
    /// $jwtclaim. Supports symmetric HS256/HS384/HS512 and asymmetric RS256/RS384/RS512 and
    /// ES256/ES384/ES512. The signing key (an HMAC secret or a PEM private key) may be given inline via
    /// 'key', or loaded through the shared SourceReader from a file, environment variable, or stored
    /// variable using the same source/path/name/encoding parameters as $read. Automatically adds 'iat'
    /// (issued-at) and, when expiresIn &gt; 0, 'exp' — without overriding any claim already present in 'claims'.
    /// </summary>
    public sealed class JwtSignMethod : MethodBase
    {
        public JwtSignMethod(ParameterExtractorService p, ILogger l, IRuntimeOperationIdProvider op, IVariableManager v, Lazy<IPlaceholderResolverService> r, ISessionManager sessionManager)
            : base(p, l, op, v, r, sessionManager)
        {
        }

        public override string Name => "jwtsign";

        public override async Task<string> ExecuteAsync(string parameters, string sessionId, CancellationToken token)
        {
            string variableName = string.Empty;
            bool isGlobal = false;

            try
            {
                var claimsRaw = await _params.ExtractStringAsync(parameters, "claims", string.Empty, sessionId, token);
                var key = await _params.ExtractStringAsync(parameters, "key", string.Empty, sessionId, token) ?? string.Empty;
                var source = await _params.ExtractStringAsync(parameters, "source", string.Empty, sessionId, token) ?? string.Empty;
                var path = await _params.ExtractStringAsync(parameters, "path", string.Empty, sessionId, token) ?? string.Empty;
                var name = await _params.ExtractStringAsync(parameters, "name", string.Empty, sessionId, token) ?? string.Empty;
                var algorithm = (await _params.ExtractStringAsync(parameters, "algorithm", "HS256", sessionId, token)).ToUpperInvariant();
                var expiresIn = await _params.ExtractNumberAsync(parameters, "expiresIn", 0, sessionId, token);
                variableName = await _params.ExtractStringAsync(parameters, "variable", string.Empty, sessionId, token);
                isGlobal = await _params.ExtractBoolAsync(parameters, "isGlobal", false, sessionId, token);

                if (string.IsNullOrWhiteSpace(claimsRaw))
                {
                    await _logger.LogAsync(_op.OperationId, "jwtsign failed. 'claims' (a JSON object) is required.", LPSLoggingLevel.Error, token);
                    await StoreTypedVariableAsync(variableName, BuildValueToken(string.Empty, "string"), "string", token, isGlobal, sessionId);
                    return string.Empty;
                }

                if (!TryGetHashAlgorithm(algorithm, out var hashAlgorithm))
                {
                    await _logger.LogAsync(_op.OperationId, $"jwtsign failed. Algorithm '{algorithm}' is not supported. Use HS256/384/512, RS256/384/512, or ES256/384/512.", LPSLoggingLevel.Error, token);
                    await StoreTypedVariableAsync(variableName, BuildValueToken(string.Empty, "string"), "string", token, isGlobal, sessionId);
                    return string.Empty;
                }

                bool isHmac = algorithm.StartsWith("HS", StringComparison.Ordinal);

                JObject claims;
                try
                {
                    claims = JObject.Parse(claimsRaw);
                }
                catch (Exception ex)
                {
                    await _logger.LogAsync(_op.OperationId, $"jwtsign failed. 'claims' is not valid JSON: {ex.Message}", LPSLoggingLevel.Error, token);
                    await StoreTypedVariableAsync(variableName, BuildValueToken(string.Empty, "string"), "string", token, isGlobal, sessionId);
                    return string.Empty;
                }

                long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                if (claims["iat"] == null) claims["iat"] = now;
                if (expiresIn > 0 && claims["exp"] == null) claims["exp"] = now + expiresIn;

                var header = new JObject { ["alg"] = algorithm, ["typ"] = "JWT" };

                string headerPart = Base64UrlEncode(Encoding.UTF8.GetBytes(header.ToString(Formatting.None)));
                string payloadPart = Base64UrlEncode(Encoding.UTF8.GetBytes(claims.ToString(Formatting.None)));
                string signingInput = headerPart + "." + payloadPart;
                byte[] signingBytes = Encoding.UTF8.GetBytes(signingInput);

                // Resolve the signing key material: an inline 'key' (HMAC secret or PEM), or — when a source
                // is given — read it via the shared SourceReader from a file, an environment variable, or a
                // stored variable (source=file|env|variable, path=..., name=...), the same way $read does.
                string keyMaterial;
                if (!string.IsNullOrWhiteSpace(source) || !string.IsNullOrWhiteSpace(path) || !string.IsNullOrWhiteSpace(name))
                {
                    keyMaterial = await SourceReader.ReadAsync(_params, _session, _variables, parameters, sessionId, token);
                }
                else
                {
                    keyMaterial = key;
                }

                if (string.IsNullOrWhiteSpace(keyMaterial))
                {
                    var reason = isHmac
                        ? "a signing 'key' is required (inline 'key', or a source such as source=env, name=...)"
                        : $"'{algorithm}' requires a private key (a PEM via path=..., a source such as source=env/name=..., or an inline PEM 'key')";
                    await _logger.LogAsync(_op.OperationId, $"jwtsign failed. {reason}.", LPSLoggingLevel.Error, token);
                    await StoreTypedVariableAsync(variableName, BuildValueToken(string.Empty, "string"), "string", token, isGlobal, sessionId);
                    return string.Empty;
                }

                byte[] signature;
                if (isHmac)
                {
                    using HMAC signer = algorithm switch
                    {
                        "HS256" => new HMACSHA256(Encoding.UTF8.GetBytes(keyMaterial)),
                        "HS384" => new HMACSHA384(Encoding.UTF8.GetBytes(keyMaterial)),
                        _ => new HMACSHA512(Encoding.UTF8.GetBytes(keyMaterial)),
                    };
                    signature = signer.ComputeHash(signingBytes);
                }
                else if (algorithm.StartsWith("RS", StringComparison.Ordinal))
                {
                    using var rsa = RSA.Create();
                    rsa.ImportFromPem(keyMaterial);
                    signature = rsa.SignData(signingBytes, hashAlgorithm, RSASignaturePadding.Pkcs1);
                }
                else
                {
                    using var ecdsa = ECDsa.Create();
                    ecdsa.ImportFromPem(keyMaterial);
                    // Default output is IEEE P1363 (raw r||s) — exactly the format JWS ES* requires.
                    signature = ecdsa.SignData(signingBytes, hashAlgorithm);
                }

                string jwt = signingInput + "." + Base64UrlEncode(signature);

                await StoreTypedVariableAsync(variableName, BuildValueToken(jwt, "string"), "string", token, isGlobal, sessionId);
                return jwt;
            }
            catch (Exception ex)
            {
                await _logger.LogAsync(_op.OperationId, $"jwtsign failed. {ex}", LPSLoggingLevel.Error, token);
                await StoreTypedVariableAsync(variableName, BuildValueToken(string.Empty, "string"), "string", token, isGlobal, sessionId);
                return string.Empty;
            }
        }

        private static bool TryGetHashAlgorithm(string algorithm, out HashAlgorithmName hash)
        {
            switch (algorithm)
            {
                case "HS256": case "RS256": case "ES256": hash = HashAlgorithmName.SHA256; return true;
                case "HS384": case "RS384": case "ES384": hash = HashAlgorithmName.SHA384; return true;
                case "HS512": case "RS512": case "ES512": hash = HashAlgorithmName.SHA512; return true;
                default: hash = default; return false;
            }
        }

        private static string Base64UrlEncode(byte[] input) =>
            Convert.ToBase64String(input).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }
}
