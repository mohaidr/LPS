using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using LPS.Domain.Common;
using LPS.Domain.Common.Interfaces;
using LPS.Infrastructure.VariableServices.GlobalVariableManager;

namespace LPS.Infrastructure.PlaceHolderService.Methods
{
    /// <summary>
    /// $hmac(value=..., key=..., algorithm=SHA256, encoding=hex|base64, variable=name)
    /// Computes a keyed HMAC of 'value' using the shared secret 'key'. Same shape as $hash, but the
    /// digest proves possession of the secret. Output is lowercase hex (default) or base64.
    /// </summary>
    public sealed class HmacMethod : MethodBase
    {
        public HmacMethod(ParameterExtractorService p, ILogger l, IRuntimeOperationIdProvider op, IVariableManager v, Lazy<IPlaceholderResolverService> r)
            : base(p, l, op, v, r)
        {
        }

        public override string Name => "hmac";

        public override async Task<string> ExecuteAsync(string parameters, string sessionId, CancellationToken token)
        {
            string variableName = string.Empty;

            try
            {
                var value = await _params.ExtractStringAsync(parameters, "value", string.Empty, sessionId, token) ?? string.Empty;
                var key = await _params.ExtractStringAsync(parameters, "key", string.Empty, sessionId, token) ?? string.Empty;
                var algorithm = (await _params.ExtractStringAsync(parameters, "algorithm", "SHA256", sessionId, token)).ToUpperInvariant();
                var encoding = (await _params.ExtractStringAsync(parameters, "encoding", "hex", sessionId, token)).ToLowerInvariant();
                variableName = await _params.ExtractStringAsync(parameters, "variable", string.Empty, sessionId, token);

                if (string.IsNullOrEmpty(key))
                {
                    await _logger.LogAsync(_op.OperationId, "hmac failed. A 'key' (shared secret) is required.", LPSLoggingLevel.Error, token);
                    await StoreStringVariableAsync(variableName, string.Empty, token);
                    return string.Empty;
                }

                var keyBytes = Encoding.UTF8.GetBytes(key);
                using HMAC hmac = algorithm switch
                {
                    "SHA1" => new HMACSHA1(keyBytes),
                    "SHA256" => new HMACSHA256(keyBytes),
                    "SHA384" => new HMACSHA384(keyBytes),
                    "SHA512" => new HMACSHA512(keyBytes),
                    _ => null
                };

                if (hmac == null)
                {
                    await _logger.LogAsync(_op.OperationId, $"hmac failed. Unsupported algorithm '{algorithm}'. Use SHA1, SHA256, SHA384, or SHA512.", LPSLoggingLevel.Error, token);
                    await StoreStringVariableAsync(variableName, string.Empty, token);
                    return string.Empty;
                }

                byte[] mac = hmac.ComputeHash(Encoding.UTF8.GetBytes(value));

                string result;
                if (encoding == "base64")
                {
                    result = Convert.ToBase64String(mac);
                }
                else
                {
                    if (encoding != "hex")
                        await _logger.LogAsync(_op.OperationId, $"hmac: unknown encoding '{encoding}', defaulting to hex.", LPSLoggingLevel.Warning, token);
                    result = BitConverter.ToString(mac).Replace("-", "").ToLowerInvariant();
                }

                await StoreStringVariableAsync(variableName, result, token);
                return result;
            }
            catch (Exception ex)
            {
                await _logger.LogAsync(_op.OperationId, $"hmac failed. {ex}", LPSLoggingLevel.Error, token);
                await StoreStringVariableAsync(variableName, string.Empty, token);
                return string.Empty;
            }
        }
    }
}
