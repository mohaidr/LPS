
using LPS.Domain.Common.Interfaces;
using LPS.Infrastructure.Common;
using LPS.Infrastructure.LPSClients.CachService;
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace LPS.Infrastructure.VariableServices.PlaceHolderService
{
    internal partial class PlaceholderProcessor
    {
        private async Task<string> ReadFileAsync(string parameters, string sessionId, CancellationToken token)
        {
            string variableName = string.Empty;
            try
            {
                string filePath = await _paramService.ExtractStringAsync(parameters, "path", string.Empty, sessionId, token);
                variableName = await _paramService.ExtractStringAsync(parameters, "variable", "", sessionId, token);

                if (string.IsNullOrWhiteSpace(filePath))
                {
                    await _logger.LogAsync(_runtimeOperationIdProvider.OperationId,
                        $"ReadFileAsync: Invalid path '<empty>'.", LPSLoggingLevel.Error, token);
                    await StoreVariableIfNeededAsync(variableName, string.Empty, token);
                    return string.Empty;
                }

                string fullPath = Path.GetFullPath(filePath, AppConstants.EnvironmentCurrentDirectory);
                string pathCacheKey = $"{CachePrefixes.Path}{fullPath}";

                if (_memoryCacheService.TryGetItem(pathCacheKey, out string cachedContent))
                {
                    return cachedContent;
                }

                if (!File.Exists(fullPath))
                {
                    await _logger.LogAsync(_runtimeOperationIdProvider.OperationId,
                        $"ReadFileAsync: File '{fullPath}' does not exist.", LPSLoggingLevel.Warning, token);
                    await StoreVariableIfNeededAsync(variableName, string.Empty, token);
                    return string.Empty;
                }

                using (await _locker.LockAsync(fullPath, token))
                {
                    try
                    {
                        using var reader = new StreamReader(fullPath, Encoding.UTF8);
                        string fileContent = await reader.ReadToEndAsync();
                        await _memoryCacheService.SetItemAsync(pathCacheKey, fileContent, TimeSpan.MaxValue);
                        await StoreVariableIfNeededAsync(variableName, fileContent ?? string.Empty, token);
                        return fileContent ?? string.Empty;
                    }
                    catch (Exception ex)
                    {
                        await _logger.LogAsync(_runtimeOperationIdProvider.OperationId,
                            $"Error reading file '{fullPath}': {ex}", LPSLoggingLevel.Error, token);
                        await StoreVariableIfNeededAsync(variableName, string.Empty, token);
                        return string.Empty;
                    }
                }
            }
            catch (Exception exOuter)
            {
                await _logger.LogAsync(_runtimeOperationIdProvider.OperationId,
                    $"ReadFileAsync outer failure. {exOuter}", LPSLoggingLevel.Error, token);
                await StoreVariableIfNeededAsync(variableName, string.Empty, token);
                return string.Empty;
            }
        }

    }
}
