using LPS.Domain.Common.Interfaces;
using LPS.Domain.Common;
using LPS.Infrastructure.Caching;
using LPS.Infrastructure.Common;
using LPS.Infrastructure.LPSClients.CachService;
using LPS.Infrastructure.LPSClients.SessionManager;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using LPS.Infrastructure.VariableServices.GlobalVariableManager;
using LPS.Infrastructure.VariableServices.VariableHolders;
using LPS.Domain.Domain.Common.Enums;
using AsyncKeyedLock;
using System.Security.Claims;

namespace LPS.Infrastructure.VariableServices.PlaceHolderService
{
    internal class PlaceholderProcessor
    {
        private readonly ISessionManager _sessionManager;
        private readonly IVariableManager _variableManager;
        private readonly ICacheService<string> _memoryCacheService;
        private readonly IRuntimeOperationIdProvider _runtimeOperationIdProvider;
        private readonly ILogger _logger;
        private readonly ParameterExtractorService _paramService;
        IPlaceholderResolverService _placeholderResolverService;
        private static readonly AsyncKeyedLocker<string> _locker = new();
        public PlaceholderProcessor(
            ParameterExtractorService paramService,
            ISessionManager sessionManager,
            ICacheService<string> memoryCacheService,
            IVariableManager variableManager,
            IPlaceholderResolverService placeholderResolverService,
            IRuntimeOperationIdProvider runtimeOperationIdProvider,
            ILogger logger)
        {
            _sessionManager = sessionManager;
            _memoryCacheService = memoryCacheService;
            _variableManager = variableManager;
            _placeholderResolverService = placeholderResolverService;
            _runtimeOperationIdProvider = runtimeOperationIdProvider;
            _logger = logger;
            _paramService = paramService;
        }


        public async Task<string> ProcessPlaceholderAsync(string placeholder, string sessionId, CancellationToken token)
        {
            int openParenIndex = placeholder.IndexOf('(');
            if (openParenIndex != -1)
            {
                return await ProcessMethodAsync(placeholder, sessionId, token);
            }
            else
            {
                return await ProcessVariableAsync(placeholder, sessionId, token);
            }
        }

        //support storing the variable generated in a global variable to be reused
        private async Task<string> ProcessMethodAsync(string placeholder, string sessionId, CancellationToken token)
        {
            int openParenIndex = placeholder.IndexOf('(');
            string functionName = placeholder.Substring(0, openParenIndex).Trim();
            string parameters = placeholder.Substring(openParenIndex + 1, placeholder.Length - openParenIndex - 2).Trim();
            return functionName.ToLowerInvariant() switch
            {
                "random" => await GenerateRandomStringAsync(parameters, sessionId, token),
                "randomnumber" => await GenerateRandomNumberAsync(parameters, sessionId, token),
                "timestamp" or "datetime" => await GenerateTimestampAsync(parameters, sessionId, token),
                "guid" => await GenerateGuidAsync(parameters, sessionId, token),
                "urlencode" => await UrlEncodeAsync(parameters, sessionId, token),
                "urldecode" => await UrlDecodeAsync(parameters, sessionId, token),       // <-- added
                "base64encode" => await Base64EncodeAsync(parameters, sessionId, token),
                "base64decode" => await Base64DecodeAsync(parameters, sessionId, token), // <-- added
                "hash" => await GenerateHashAsync(parameters, sessionId, token),
                "read" => await ReadFileAsync(parameters, sessionId, token),
                "loopcounter" or "iterate" => await IterateAsync(parameters, sessionId, token),
                "uuid" => await GenerateUuidAsync(parameters, sessionId, token),
                "format" => await FormatTemplateAsync(parameters, sessionId, token),
                "jwtclaim" => await ExtractJwtClaimAsync(parameters, sessionId, token),
                "generateemail" => await GenerateEmailAsync(parameters, sessionId, token),

                _ => await ProcessVariableAsync(functionName, sessionId, token)
            } ;
        }
        private async Task<string> ProcessVariableAsync(string placeholder, string sessionId, CancellationToken token)
        {
            string variableName = placeholder;
            string path = null;

            if (placeholder.Contains('.') || placeholder.Contains('/') || placeholder.Contains('['))
            {
                int splitIndex = placeholder.IndexOfAny(['.', '/', '[']);
                variableName = placeholder.Substring(0, splitIndex);
                path = placeholder.Substring(splitIndex);
            }

            var variableHolder = await _sessionManager.GetVariableAsync(sessionId, variableName, token)
                              ?? await _variableManager.GetAsync(variableName, token);

            if (variableHolder == null)
            {
                await _logger.LogAsync(_runtimeOperationIdProvider.OperationId, $"Variable '{variableName}' not found.", LPSLoggingLevel.Warning, token);
                return $"${{{variableName}{path}}}";
            }

            string resolvedValue = string.Empty;

            if (string.IsNullOrWhiteSpace(path))
            {
                resolvedValue = await variableHolder.GetRawValueAsync(token);
            }
            else
            {
                if (variableHolder is IObjectVariableHolder)
                {
                    resolvedValue = await ((IObjectVariableHolder)variableHolder).GetValueAsync(path, sessionId, token);
                }
                else 
                {
                    resolvedValue = await variableHolder.GetRawValueAsync(token);
                }
            }
            return resolvedValue;
        }
        // Helpers (local/private). Keep logs concise and avoid dumping huge/secret values.
        private static string TruncateForLog(string? value, int max = 128)
            => string.IsNullOrEmpty(value) ? "<empty>" : (value.Length <= max ? value : value.Substring(0, max) + $"...(+{value.Length - max} chars)");

        private static string MaskJwtForLog(string? jwt)
        {
            if (string.IsNullOrWhiteSpace(jwt)) return "<empty>";
            var parts = jwt.Split('.');
            var head = parts.Length > 0 ? parts[0] : "";
            var payload = parts.Length > 1 ? parts[1] : "";
            return $"header({TruncateForLog(head, 32)}), payload({TruncateForLog(payload, 32)}), sig({(parts.Length > 2 ? "<present>" : "<missing>")})";
        }

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
                await _logger.LogAsync(_runtimeOperationIdProvider.OperationId,
                    $"Failed GenerateRandomStringAsync (length param). {ex}", LPSLoggingLevel.Error, token);
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
                await _logger.LogAsync(_runtimeOperationIdProvider.OperationId,
                    $"Failed GenerateRandomNumberAsync (min/max). {ex}", LPSLoggingLevel.Error, token);
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
                await _logger.LogAsync(_runtimeOperationIdProvider.OperationId,
                    $"Failed GenerateTimestampAsync (format/offset). {ex}", LPSLoggingLevel.Error, token);
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
                await _logger.LogAsync(_runtimeOperationIdProvider.OperationId,
                    $"Failed GenerateGuidAsync. {ex}", LPSLoggingLevel.Error, token);
                await StoreVariableIfNeededAsync(variableName, string.Empty, token);
                return string.Empty;
            }
        }

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
                    await _logger.LogAsync(_runtimeOperationIdProvider.OperationId,
                        $"Unsupported hash algorithm '{algorithm}' for value '{TruncateForLog(value)}'.", LPSLoggingLevel.Error, token);
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
                // Log both algorithm and a truncated value
                string valueSafe = "<see above>"; // value may not be in scope if failure earlier
                await _logger.LogAsync(_runtimeOperationIdProvider.OperationId,
                    $"Failed GenerateHashAsync. Algorithm may be invalid. {ex}", LPSLoggingLevel.Error, token);
                await StoreVariableIfNeededAsync(variableName, string.Empty, token);
                return string.Empty;
            }
        }
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

                var counterNameCachePart = !string.IsNullOrEmpty(counterName) ? $"_{counterName.Trim()}" : string.Empty;
                if (startValue >= endValue)
                {
                    await _logger.LogAsync(_runtimeOperationIdProvider.OperationId,
                        $"IterateAsync invalid range: start({startValue}) >= end({endValue}).", LPSLoggingLevel.Error, token);
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
                            await _logger.LogAsync(_runtimeOperationIdProvider.OperationId,
                                $"IterateAsync key '{cacheKey}': reset to start '{startValue}' (out of bounds).",
                                LPSLoggingLevel.Verbose, token);
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
                await _logger.LogAsync(_runtimeOperationIdProvider.OperationId,
                    $"Failed IterateAsync. {ex}", LPSLoggingLevel.Error, token);
                await StoreVariableIfNeededAsync(variableName, string.Empty, token);
                return string.Empty;
            }
        }

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
                await _logger.LogAsync(_runtimeOperationIdProvider.OperationId,
                    $"Failed UrlEncodeAsync for value '{TruncateForLog(value)}'. {ex}", LPSLoggingLevel.Error, token);
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
                await _logger.LogAsync(_runtimeOperationIdProvider.OperationId,
                    $"Failed UrlDecodeAsync for value '{TruncateForLog(value)}'. {ex}", LPSLoggingLevel.Error, token);
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
                await _logger.LogAsync(_runtimeOperationIdProvider.OperationId,
                    $"Failed Base64EncodeAsync for value '{TruncateForLog(value)}'. {ex}", LPSLoggingLevel.Error, token);
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
                await _logger.LogAsync(_runtimeOperationIdProvider.OperationId,
                    $"Failed Base64DecodeAsync for value '{TruncateForLog(value)}'. {ex}", LPSLoggingLevel.Error, token);
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
                    await _logger.LogAsync(_runtimeOperationIdProvider.OperationId,
                        $"ExtractJwtClaimAsync: Invalid JWT format. Token: {MaskJwtForLog(tokenStr)}", LPSLoggingLevel.Error, token);
                    await StoreVariableIfNeededAsync(variableName, string.Empty, token);
                    return string.Empty;
                }

                string payloadJson = Encoding.UTF8.GetString(Convert.FromBase64String(parts[1].PadRight(parts[1].Length + (4 - parts[1].Length % 4) % 4, '=')));
                var dict = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(payloadJson);

                string result = (dict != null && dict.TryGetValue(claim, out var valueObj)) ? valueObj?.ToString() ?? string.Empty : string.Empty;
                await StoreVariableIfNeededAsync(variableName, result, token);
                return result;
            }
            catch (Exception ex)
            {
                await _logger.LogAsync(_runtimeOperationIdProvider.OperationId,
                    $"Failed ExtractJwtClaimAsync for claim '{claim}' on token {MaskJwtForLog(tokenStr)}. {ex}",
                    LPSLoggingLevel.Error, token);
                await StoreVariableIfNeededAsync(variableName, string.Empty, token);
                return string.Empty;
            }
        }

        private async Task<string> GenerateEmailAsync(string parameters, string sessionId, CancellationToken token)
        {
            string variableName = string.Empty;
            try
            {
                string prefix = await _paramService.ExtractStringAsync(parameters, "prefix", "user", sessionId, token);
                string domain = await _paramService.ExtractStringAsync(parameters, "domain", "example.com", sessionId, token);
                variableName = await _paramService.ExtractStringAsync(parameters, "variable", "", sessionId, token);
                string uniquePart = Guid.NewGuid().ToString("N").Substring(0, 8);
                string email = $"{prefix}-{uniquePart}@{domain}";
                await StoreVariableIfNeededAsync(variableName, email, token);
                return email;
            }
            catch (Exception ex)
            {
                await _logger.LogAsync(_runtimeOperationIdProvider.OperationId,
                    $"Failed GenerateEmailAsync (prefix/domain). {ex}", LPSLoggingLevel.Error, token);
                await StoreVariableIfNeededAsync(variableName, string.Empty, token);
                return string.Empty;
            }
        }

        private async Task<string> FormatTemplateAsync(string parameters, string sessionId, CancellationToken token)
        {
            string variableName = string.Empty;
            string template = string.Empty;
            string args = string.Empty;
            try
            {
                template = await _paramService.ExtractStringAsync(parameters, "template", string.Empty, sessionId, token);
                args = await _paramService.ExtractStringAsync(parameters, "args", string.Empty, sessionId, token);
                variableName = await _paramService.ExtractStringAsync(parameters, "variable", "", sessionId, token);

                string result = string.Format(template, args.Split(",").ToArray());
                result = await _placeholderResolverService.ResolvePlaceholdersAsync<string>(result, string.Empty, token);

                await StoreVariableIfNeededAsync(variableName, result, token);
                return result;
            }
            catch (Exception ex)
            {
                await _logger.LogAsync(_runtimeOperationIdProvider.OperationId,
                    $"Failed FormatTemplateAsync. template='{TruncateForLog(template)}', args='{TruncateForLog(args)}'. {ex}",
                    LPSLoggingLevel.Error, token);
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
                await _logger.LogAsync(_runtimeOperationIdProvider.OperationId,
                    $"Failed GenerateUuidAsync. {ex}", LPSLoggingLevel.Error, token);
                await StoreVariableIfNeededAsync(variableName, string.Empty, token);
                return string.Empty;
            }
        }

        private async Task StoreVariableIfNeededAsync(string variableName, string value, CancellationToken token)
        {
            if (string.IsNullOrWhiteSpace(variableName))
                return;

            var holder = await new StringVariableHolder.VBuilder(_placeholderResolverService, _logger, _runtimeOperationIdProvider)
                .WithType(VariableType.String)
                .WithRawValue(value)
                .SetGlobal()
                .BuildAsync(token);

            await _variableManager.PutAsync(variableName, holder, token);
        }

    }
}
