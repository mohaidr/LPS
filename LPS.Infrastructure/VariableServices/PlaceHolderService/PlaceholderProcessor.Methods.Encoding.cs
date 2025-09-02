
using LPS.Domain.Common.Interfaces;
using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace LPS.Infrastructure.VariableServices.PlaceHolderService
{
    internal partial class PlaceholderProcessor
    {
        private async Task<string> UrlEncodeAsync(string parameters, string sessionId, CancellationToken token)
        {
            string variableName = string.Empty;
            string value = string.Empty;
            try
            {
                value = await _paramService.ExtractStringAsync(parameters, "value", string.Empty, sessionId, token);
                variableName = await _paramService.ExtractStringAsync(parameters, "variable", "", sessionId, token);
                string result = string.IsNullOrEmpty(value) ? string.Empty : Uri.EscapeDataString(value);
                await StoreVariableIfNeededAsync(variableName, result, token);
                return result;
            }
            catch (Exception ex)
            {
                await _logger.LogAsync(_runtimeOperationIdProvider.OperationId, $"Failed UrlEncodeAsync for value '{TruncateForLog(value)}'. {ex}", LPSLoggingLevel.Error, token);
                await StoreVariableIfNeededAsync(variableName, string.Empty, token);
                return string.Empty;
            }
        }

        private async Task<string> UrlDecodeAsync(string parameters, string sessionId, CancellationToken token)
        {
            string variableName = string.Empty;
            string value = string.Empty;
            try
            {
                value = await _paramService.ExtractStringAsync(parameters, "value", string.Empty, sessionId, token);
                variableName = await _paramService.ExtractStringAsync(parameters, "variable", "", sessionId, token);
                string result = string.IsNullOrEmpty(value) ? string.Empty : Uri.UnescapeDataString(value);
                await StoreVariableIfNeededAsync(variableName, result, token);
                return result;
            }
            catch (Exception ex)
            {
                await _logger.LogAsync(_runtimeOperationIdProvider.OperationId, $"Failed UrlDecodeAsync for value '{TruncateForLog(value)}'. {ex}", LPSLoggingLevel.Error, token);
                await StoreVariableIfNeededAsync(variableName, string.Empty, token);
                return string.Empty;
            }
        }

        private async Task<string> Base64EncodeAsync(string parameters, string sessionId, CancellationToken token)
        {
            string variableName = string.Empty;
            string value = string.Empty;
            try
            {
                value = await _paramService.ExtractStringAsync(parameters, "value", string.Empty, sessionId, token);
                variableName = await _paramService.ExtractStringAsync(parameters, "variable", "", sessionId, token);
                string result = string.IsNullOrEmpty(value) ? string.Empty : Convert.ToBase64String(Encoding.UTF8.GetBytes(value));
                await StoreVariableIfNeededAsync(variableName, result, token);
                return result;
            }
            catch (Exception ex)
            {
                await _logger.LogAsync(_runtimeOperationIdProvider.OperationId, $"Failed Base64EncodeAsync for value '{TruncateForLog(value)}'. {ex}", LPSLoggingLevel.Error, token);
                await StoreVariableIfNeededAsync(variableName, string.Empty, token);
                return string.Empty;
            }
        }

        private async Task<string> Base64DecodeAsync(string parameters, string sessionId, CancellationToken token)
        {
            string variableName = string.Empty;
            string value = string.Empty;
            try
            {
                value = await _paramService.ExtractStringAsync(parameters, "value", string.Empty, sessionId, token);
                variableName = await _paramService.ExtractStringAsync(parameters, "variable", "", sessionId, token);

                if (string.IsNullOrEmpty(value))
                {
                    await StoreVariableIfNeededAsync(variableName, string.Empty, token);
                    return string.Empty;
                }

                // tolerate missing padding (like JWT segments)
                string padded = value.PadRight(value.Length + (4 - value.Length % 4) % 4, '=');
                string result = Encoding.UTF8.GetString(Convert.FromBase64String(padded));
                await StoreVariableIfNeededAsync(variableName, result, token);
                return result;
            }
            catch (Exception ex)
            {
                await _logger.LogAsync(_runtimeOperationIdProvider.OperationId, $"Failed Base64DecodeAsync for value '{TruncateForLog(value)}'. {ex}", LPSLoggingLevel.Error, token);
                await StoreVariableIfNeededAsync(variableName, string.Empty, token);
                return string.Empty;
            }
        }
    }
}
