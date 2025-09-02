
using LPS.Domain.Common.Interfaces;
using LPS.Infrastructure.LPSClients.CachService;
using System;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace LPS.Infrastructure.VariableServices.PlaceHolderService
{
    internal partial class PlaceholderProcessor
    {
        private async Task<string> GenerateRandomStringAsync(string parameters, string sessionId, CancellationToken token)
        {
            string variableName = string.Empty;
            try
            {
                int length = await _paramService.ExtractNumberAsync(parameters, "length", 10, sessionId, token);
                variableName = await _paramService.ExtractStringAsync(parameters, "variable", "", sessionId, token);

                const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
                var random = new Random();
                string result = new string(Enumerable.Repeat(chars, length).Select(s => s[random.Next(s.Length)]).ToArray());
                await StoreVariableIfNeededAsync(variableName, result, token);
                return result;
            }
            catch (Exception ex)
            {
                await _logger.LogAsync(_runtimeOperationIdProvider.OperationId, $"Failed GenerateRandomStringAsync (length param). {ex}", LPSLoggingLevel.Error, token);
                await StoreVariableIfNeededAsync(variableName, string.Empty, token);
                return string.Empty;
            }
        }

        private async Task<string> GenerateRandomNumberAsync(string parameters, string sessionId, CancellationToken token)
        {
            string variableName = string.Empty;
            try
            {
                int min = await _paramService.ExtractNumberAsync(parameters, "min", 1, sessionId, token);
                int max = await _paramService.ExtractNumberAsync(parameters, "max", 100, sessionId, token);
                variableName = await _paramService.ExtractStringAsync(parameters, "variable", "", sessionId, token);

                var random = new Random();
                string result = random.Next(min, max + 1).ToString();
                await StoreVariableIfNeededAsync(variableName, result, token);
                return result;
            }
            catch (Exception ex)
            {
                await _logger.LogAsync(_runtimeOperationIdProvider.OperationId, $"Failed GenerateRandomNumberAsync (min/max). {ex}", LPSLoggingLevel.Error, token);
                await StoreVariableIfNeededAsync(variableName, string.Empty, token);
                return string.Empty;
            }
        }

        private async Task<string> GenerateTimestampAsync(string parameters, string sessionId, CancellationToken token)
        {
            string variableName = string.Empty;
            try
            {
                string format = await _paramService.ExtractStringAsync(parameters, "format", "yyyy-MM-ddTHH:mm:ss", sessionId, token);
                variableName = await _paramService.ExtractStringAsync(parameters, "variable", "", sessionId, token);
                int offsetHours = await _paramService.ExtractNumberAsync(parameters, "offsetHours", 0, sessionId, token);

                DateTime resultTime = DateTime.UtcNow.AddHours(offsetHours);
                string result = resultTime.ToString(format);
                await StoreVariableIfNeededAsync(variableName, result, token);
                return result;
            }
            catch (Exception ex)
            {
                await _logger.LogAsync(_runtimeOperationIdProvider.OperationId, $"Failed GenerateTimestampAsync (format/offset). {ex}", LPSLoggingLevel.Error, token);
                await StoreVariableIfNeededAsync(variableName, string.Empty, token);
                return string.Empty;
            }
        }

        private async Task<string> GenerateGuidAsync(string parameters, string sessionId, CancellationToken token)
        {
            string variableName = string.Empty;
            try
            {
                variableName = await _paramService.ExtractStringAsync(parameters, "variable", "", sessionId, token);
                string result = Guid.NewGuid().ToString();
                await StoreVariableIfNeededAsync(variableName, result, token);
                return result;
            }
            catch (Exception ex)
            {
                await _logger.LogAsync(_runtimeOperationIdProvider.OperationId, $"Failed GenerateGuidAsync. {ex}", LPSLoggingLevel.Error, token);
                await StoreVariableIfNeededAsync(variableName, string.Empty, token);
                return string.Empty;
            }
        }

        private async Task<string> IterateAsync(string parameters, string sessionId, CancellationToken token)
        {
            string variableName = string.Empty;
            try
            {
                var startValue = await _paramService.ExtractNumberAsync(parameters, "start", 0, sessionId, token);
                var endValue = await _paramService.ExtractNumberAsync(parameters, "end", 100000, sessionId, token);
                var counterName = await _paramService.ExtractStringAsync(parameters, "counter", string.Empty, sessionId, token);
                var step = await _paramService.ExtractNumberAsync(parameters, "step", 1, sessionId, token);
                variableName = await _paramService.ExtractStringAsync(parameters, "variable", "", sessionId, token);

                var counterNameCachePart = !string.IsNullOrEmpty(counterName) ? $"_{{counterName.Trim()}}" : string.Empty;
                if (startValue >= endValue)
                {
                    await _logger.LogAsync(_runtimeOperationIdProvider.OperationId, $"IterateAsync invalid range: start({startValue}) >= end({endValue}).", LPSLoggingLevel.Error, token);
                    await StoreVariableIfNeededAsync(variableName, string.Empty, token);
                    return string.Empty;
                }

                string cacheKey = string.IsNullOrEmpty(sessionId) || !int.TryParse(sessionId, out _)
                    ? $"{CachePrefixes.GlobalCounter}{startValue}_{endValue}{counterNameCachePart}"
                    : $"{CachePrefixes.SessionCounter}{sessionId}_{startValue}_{endValue}{counterNameCachePart}";

                using (await _locker.LockAsync(cacheKey, token))
                {
                    if (!_memoryCacheService.TryGetItem(cacheKey, out string currentValueString) ||
                        !int.TryParse(currentValueString, out int currentValue))
                    {
                        currentValue = startValue;
                    }
                    else
                    {
                        currentValue += step;
                        if (currentValue > endValue || currentValue < startValue)
                        {
                            currentValue = startValue; // Restart
                            await _logger.LogAsync(_runtimeOperationIdProvider.OperationId, $"IterateAsync key '{cacheKey}': reset to start '{startValue}' (out of bounds).", LPSLoggingLevel.Verbose, token);
                        }
                    }

                    string result = currentValue.ToString();
                    await _memoryCacheService.SetItemAsync(cacheKey, result, TimeSpan.MaxValue);
                    await StoreVariableIfNeededAsync(variableName, result, token);
                    return result;
                }
            }
            catch (Exception ex)
            {
                await _logger.LogAsync(_runtimeOperationIdProvider.OperationId, $"Failed IterateAsync. {ex}", LPSLoggingLevel.Error, token);
                await StoreVariableIfNeededAsync(variableName, string.Empty, token);
                return string.Empty;
            }
        }

        private async Task<string> GenerateUuidAsync(string parameters, string sessionId, CancellationToken token)
        {
            string variableName = string.Empty;
            try
            {
                string prefix = await _paramService.ExtractStringAsync(parameters, "prefix", "", sessionId, token);
                variableName = await _paramService.ExtractStringAsync(parameters, "variable", "", sessionId, token);
                string uuid = Guid.NewGuid().ToString();
                string result = $"{prefix}{uuid}";
                await StoreVariableIfNeededAsync(variableName, result, token);
                return result;
            }
            catch (Exception ex)
            {
                await _logger.LogAsync(_runtimeOperationIdProvider.OperationId, $"Failed GenerateUuidAsync. {ex}", LPSLoggingLevel.Error, token);
                await StoreVariableIfNeededAsync(variableName, string.Empty, token);
                return string.Empty;
            }
        }
    }
}
