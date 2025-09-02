
using LPS.Domain.Common.Interfaces;
using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace LPS.Infrastructure.VariableServices.PlaceHolderService
{
    internal partial class PlaceholderProcessor
    {
        private async Task<string> GenerateHashAsync(string parameters, string sessionId, CancellationToken token)
        {
            string variableName = string.Empty;
            try
            {
                string value = await _paramService.ExtractStringAsync(parameters, "value", string.Empty, sessionId, token);
                string algorithm = await _paramService.ExtractStringAsync(parameters, "algorithm", "SHA256", sessionId, token);
                variableName = await _paramService.ExtractStringAsync(parameters, "variable", "", sessionId, token);

                using var hasher = algorithm switch
                {
                    "MD5" => System.Security.Cryptography.MD5.Create(),
                    "SHA256" => System.Security.Cryptography.SHA256.Create(),
                    "SHA1" => (System.Security.Cryptography.HashAlgorithm)System.Security.Cryptography.SHA1.Create(),
                    _ => null
                };

                if (hasher == null)
                {
                    await _logger.LogAsync(_runtimeOperationIdProvider.OperationId, $"Unsupported hash algorithm '{algorithm}' for value '{TruncateForLog(value)}'.", LPSLoggingLevel.Error, token);
                    await StoreVariableIfNeededAsync(variableName, string.Empty, token);
                    return string.Empty;
                }

                byte[] hash = hasher.ComputeHash(Encoding.UTF8.GetBytes(value ?? string.Empty));
                string result = BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
                await StoreVariableIfNeededAsync(variableName, result, token);
                return result;
            }
            catch (Exception ex)
            {
                await _logger.LogAsync(_runtimeOperationIdProvider.OperationId, $"Failed GenerateHashAsync. Algorithm may be invalid. {ex}", LPSLoggingLevel.Error, token);
                await StoreVariableIfNeededAsync(variableName, string.Empty, token);
                return string.Empty;
            }
        }

        private async Task<string> ExtractJwtClaimAsync(string parameters, string sessionId, CancellationToken token)
        {
            string variableName = string.Empty;
            string tokenStr = string.Empty;
            string claim = string.Empty;

            try
            {
                tokenStr = await _paramService.ExtractStringAsync(parameters, "token", "", sessionId, token);
                claim = await _paramService.ExtractStringAsync(parameters, "claim", "", sessionId, token);
                variableName = await _paramService.ExtractStringAsync(parameters, "variable", "", sessionId, token);

                if (string.IsNullOrEmpty(tokenStr) || string.IsNullOrEmpty(claim))
                {
                    await StoreVariableIfNeededAsync(variableName, string.Empty, token);
                    return string.Empty;
                }

                string[] parts = tokenStr.Split('.');
                if (parts.Length < 2)
                {
                    await _logger.LogAsync(_runtimeOperationIdProvider.OperationId, $"ExtractJwtClaimAsync: Invalid JWT format. Token: {MaskJwtForLog(tokenStr)}", LPSLoggingLevel.Error, token);
                    await StoreVariableIfNeededAsync(variableName, string.Empty, token);
                    return string.Empty;
                }

                string payloadJson = Encoding.UTF8.GetString(Convert.FromBase64String(parts[1].PadRight(parts[1].Length + (4 - parts[1].Length % 4) % 4, '=')));
                var dict = System.Text.Json.JsonSerializer.Deserialize<System.Collections.Generic.Dictionary<string, object>>(payloadJson);

                string result = (dict != null && dict.TryGetValue(claim, out var valueObj)) ? valueObj?.ToString() ?? string.Empty : string.Empty;
                await StoreVariableIfNeededAsync(variableName, result, token);
                return result;
            }
            catch (Exception ex)
            {
                await _logger.LogAsync(_runtimeOperationIdProvider.OperationId, $"Failed ExtractJwtClaimAsync for claim '{claim}' on token {MaskJwtForLog(tokenStr)}. {ex}", LPSLoggingLevel.Error, token);
                await StoreVariableIfNeededAsync(variableName, string.Empty, token);
                return string.Empty;
            }
        }
    }
}
