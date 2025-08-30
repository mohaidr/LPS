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


        public async Task<string> ResolvePlaceholderAsync(string placeholder, string sessionId, CancellationToken token)
        {
            if (IPlaceholderResolverService.IsSupportedPlaceHolderMethod($"${placeholder}"))
            {
                return await ResolveMethodAsync(placeholder, sessionId, token);
            }
            else
            {
                return await ResolveVariableAsync(placeholder, sessionId, token);
            }
        }

        //support storing the variable generated in a global variable to be reused
        private async Task<string> ResolveMethodAsync(string placeholder, string sessionId, CancellationToken token)
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

                _ => throw new InvalidOperationException($"Unknown function: {functionName}")
            };
        }
        private async Task<string> ResolveVariableAsync(string placeholder, string sessionId, CancellationToken token)
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
        private async Task<string> GenerateRandomStringAsync(string parameters, string sessionId, CancellationToken token)
        {
            int length = await _paramService.ExtractNumberAsync(parameters, "length", 10, sessionId, token);
            string variableName = await _paramService.ExtractStringAsync(parameters, "variable", "", sessionId, token);

            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
            var random = new Random();
            string result = new string(Enumerable.Repeat(chars, length).Select(s => s[random.Next(s.Length)]).ToArray());

            await StoreVariableIfNeededAsync(variableName, result, token);

            return result;
        }
        private async Task<string> GenerateRandomNumberAsync(string parameters, string sessionId, CancellationToken token)
        {
            int min = await _paramService.ExtractNumberAsync(parameters, "min", 1, sessionId, token);
            int max = await _paramService.ExtractNumberAsync(parameters, "max", 100, sessionId, token);
            string variableName = await _paramService.ExtractStringAsync(parameters, "variable", "", sessionId, token);

            var random = new Random();
            string result = random.Next(min, max + 1).ToString();
            await StoreVariableIfNeededAsync(variableName, result, token);


            return result;
        }
        private async Task<string> GenerateTimestampAsync(string parameters, string sessionId, CancellationToken token)
        {
            string format = await _paramService.ExtractStringAsync(parameters, "format", "yyyy-MM-ddTHH:mm:ss", sessionId, token);
            string variableName = await _paramService.ExtractStringAsync(parameters, "variable", "", sessionId, token);
            int offsetHours = await _paramService.ExtractNumberAsync(parameters, "offsetHours", 0, sessionId, token);

            // Apply offset
            DateTime utcNow = DateTime.UtcNow;
            DateTime adjustedTime = utcNow.AddHours(offsetHours);

            string result = adjustedTime.ToString(format);

            await StoreVariableIfNeededAsync(variableName, result, token);

            return result;
        }

        private async Task<string> GenerateGuidAsync(string parameters, string sessionId, CancellationToken token)
        {
            string variableName = await _paramService.ExtractStringAsync(parameters, "variable", "", sessionId, token);
            string result = Guid.NewGuid().ToString();
            await StoreVariableIfNeededAsync(variableName, result, token);


            return result;
        }
        private async Task<string> GenerateHashAsync(string parameters, string sessionId, CancellationToken token)
        {
            string value = await _paramService.ExtractStringAsync(parameters, "value", string.Empty, sessionId, token);
            string algorithm = await _paramService.ExtractStringAsync(parameters, "algorithm", "SHA256", sessionId, token);

            using var hasher = algorithm switch
            {
                "MD5" => System.Security.Cryptography.MD5.Create(),
                "SHA256" => System.Security.Cryptography.SHA256.Create(),
                "SHA1" => (System.Security.Cryptography.HashAlgorithm)System.Security.Cryptography.SHA1.Create(),
                _ => throw new InvalidOperationException($"Unsupported hash algorithm: {algorithm}")
            };

            byte[] hash = hasher.ComputeHash(Encoding.UTF8.GetBytes(value));
            return BitConverter.ToString(hash).Replace("-", "").ToLower();
        }
        private async Task<string> ReadFileAsync(string parameters, string sessionId, CancellationToken token)
        {
            string filePath = await _paramService.ExtractStringAsync(parameters, "path", string.Empty, sessionId, token);

            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("File path cannot be null or empty.", nameof(parameters));

            string fullPath = Path.GetFullPath(filePath, AppConstants.EnvironmentCurrentDirectory);
            // Check if the file content is already cached
            string pathCacheKey = $"{CachePrefixes.Path}{fullPath}";
            if (_memoryCacheService.TryGetItem(pathCacheKey, out string cachedContent))
            {
                return cachedContent;
            }

            if (!File.Exists(fullPath))
            {
                await _logger.LogAsync(_runtimeOperationIdProvider.OperationId, $"File '{fullPath}' does not exist.", LPSLoggingLevel.Warning, token);
                return string.Empty;
            }
            using (await _locker.LockAsync(fullPath, token))
            {
                try
                {
                    using var reader = new StreamReader(fullPath, Encoding.UTF8);
                    string fileContent = await reader.ReadToEndAsync();

                    // Cache the file content for the program's lifetime
                    await _memoryCacheService.SetItemAsync(pathCacheKey, fileContent, TimeSpan.MaxValue);
                    return fileContent;
                }
                catch (Exception ex)
                {
                    await _logger.LogAsync(_runtimeOperationIdProvider.OperationId, $"Error reading file '{fullPath}': {ex.Message}", LPSLoggingLevel.Error, token);
                    throw;
                }
            }
        }
        private async Task<string> IterateAsync(string parameters, string sessionId, CancellationToken token)
        {
            // Extract parameters for start and end values
            var startValue = await _paramService.ExtractNumberAsync(parameters, "start", 0, sessionId, token);
            var endValue = await _paramService.ExtractNumberAsync(parameters, "end", 100000, sessionId, token);
            var counterName = await _paramService.ExtractStringAsync(parameters, "counter", string.Empty, sessionId, token);
            var step = await _paramService.ExtractNumberAsync(parameters, "step", 1, sessionId, token);
            var variableName = await _paramService.ExtractStringAsync(parameters, "variable", "", sessionId, token);

            var counterNameCachePart = !string.IsNullOrEmpty(counterName) ? $"_{counterName.Trim()}" : string.Empty;
            if (startValue >= endValue)
            {
                throw new ArgumentException("startValue must be less than endValue.");
            }

            // Determine cache key
            string cacheKey = string.IsNullOrEmpty(sessionId) || !int.TryParse(sessionId, out _)
                ? $"{CachePrefixes.GlobalCounter}{startValue}_{endValue}{counterNameCachePart}"
                : $"{CachePrefixes.SessionCounter}{sessionId}_{startValue}_{endValue}{counterNameCachePart}";

            using (await _locker.LockAsync(cacheKey, token))
            {
                try
                {
                    // Retrieve the current value from the cache or initialize to startValue
                    if (!_memoryCacheService.TryGetItem(cacheKey, out string currentValueString) || !int.TryParse(currentValueString, out int currentValue))
                    {
                        currentValue = startValue;
                    }
                    else
                    {
                        currentValue += step;
                        if (currentValue > endValue || currentValue < startValue)
                        {
                            currentValue = startValue; // Restart counter
                            await _logger.LogAsync(
                                _runtimeOperationIdProvider.OperationId,
                                $"Cache key '{cacheKey}': Counter reset to start value '{startValue}' because current value '{currentValue}' exceeded end value '{endValue}' or fell below start value.",
                                LPSLoggingLevel.Verbose,
                                token
                            );
                        }

                    }

                    // Update the cache with the new value
                    await _memoryCacheService.SetItemAsync(cacheKey, currentValue.ToString(), TimeSpan.MaxValue);

                    await StoreVariableIfNeededAsync(variableName, currentValue.ToString(), token);



                    return currentValue.ToString();
                }
                finally
                {
                }
            }
        }
        private async Task<string> UrlEncodeAsync(string parameters, string sessionId, CancellationToken token)
        {
            string value = await _paramService.ExtractStringAsync(parameters, "value", string.Empty, sessionId, token);
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            return Uri.EscapeDataString(value);
        }
        private async Task<string> Base64EncodeAsync(string parameters, string sessionId, CancellationToken token)
        {
            string value = await _paramService.ExtractStringAsync(parameters, "value", string.Empty, sessionId, token);
            return Convert.ToBase64String(Encoding.UTF8.GetBytes(value));
        }
        private async Task<string> Base64DecodeAsync(string parameters, string sessionId, CancellationToken token)
        {
            string value = await _paramService.ExtractStringAsync(parameters, "value", string.Empty, sessionId, token);
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            try
            {
                byte[] decodedBytes = Convert.FromBase64String(value);
                return Encoding.UTF8.GetString(decodedBytes);
            }
            catch (FormatException)
            {
                throw new InvalidOperationException("Invalid Base64 string provided for decoding.");
            }
        }
        private async Task<string> UrlDecodeAsync(string parameters, string sessionId, CancellationToken token)
        {
            string value = await _paramService.ExtractStringAsync(parameters, "value", string.Empty, sessionId, token);
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            return Uri.UnescapeDataString(value);
        }

        //new ones
        private async Task<string> ExtractJwtClaimAsync(string parameters, string sessionId, CancellationToken token)
        {
            string tokenStr = await _paramService.ExtractStringAsync(parameters, "token", "", sessionId, token);
            string claim = await _paramService.ExtractStringAsync(parameters, "claim", "", sessionId, token);

            if (string.IsNullOrEmpty(tokenStr) || string.IsNullOrEmpty(claim))
                return string.Empty;

            string[] parts = tokenStr.Split('.');
            if (parts.Length < 2)
                throw new InvalidOperationException("Invalid JWT token format.");

            string payloadJson = Encoding.UTF8.GetString(Convert.FromBase64String(PadBase64(parts[1])));
            var dict = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(payloadJson);

            if (dict != null && dict.TryGetValue(claim, out var value))
                return value?.ToString() ?? string.Empty;

            return string.Empty;

            static string PadBase64(string base64)
            {
                return base64.PadRight(base64.Length + (4 - base64.Length % 4) % 4, '=');
            }
        }

        private async Task<string> GenerateEmailAsync(string parameters, string sessionId, CancellationToken token)
        {
            string prefix = await _paramService.ExtractStringAsync(parameters, "prefix", "user", sessionId, token);
            string domain = await _paramService.ExtractStringAsync(parameters, "domain", "example.com", sessionId, token);
            string variableName = await _paramService.ExtractStringAsync(parameters, "variable", "", sessionId, token);
            string uniquePart = Guid.NewGuid().ToString("N").Substring(0, 8);
            string email = $"{prefix}-{uniquePart}@{domain}";

            await StoreVariableIfNeededAsync(variableName, email, token);
            return email;
        }

        private async Task<string> FormatTemplateAsync(string parameters, string sessionId, CancellationToken token)
        {
            try
            {
                string template = await _paramService.ExtractStringAsync(parameters, "template", string.Empty, sessionId, token);
                string args = await _paramService.ExtractStringAsync(parameters, "args", string.Empty, sessionId, token);
                string result = string.Format(template, args.Split(",").ToArray());
                result = await _placeholderResolverService.ResolvePlaceholdersAsync<string>(result, string.Empty, token);
                string variableName = await _paramService.ExtractStringAsync(parameters, "variable", "", sessionId, token);
                await StoreVariableIfNeededAsync(variableName, result, token);
                return result;
            }
            catch (Exception ex)
            {
                await _logger.LogAsync(_runtimeOperationIdProvider.OperationId, $"{ex}", LPSLoggingLevel.Error, token);
                return string.Empty;
            }
        }

        private async Task<string> GenerateUuidAsync(string parameters, string sessionId, CancellationToken token)
        {
            string prefix = await _paramService.ExtractStringAsync(parameters, "prefix", "", sessionId, token);
            string variableName = await _paramService.ExtractStringAsync(parameters, "variable", "", sessionId, token);
            string uuid = Guid.NewGuid().ToString();
            string result = $"{prefix}{uuid}";
            await StoreVariableIfNeededAsync(variableName, result, token);
            return result;
        }

    }
}
