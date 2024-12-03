using LPS.Domain.Common;
using LPS.Infrastructure.LPSClients.SessionManager;
using LPS.Infrastructure.Caching;
using System;
using System.Linq;
using System.Text;
using LPS.Infrastructure.LPSClients.GlobalVariableManager;
using System.Threading.Tasks;
using System.Threading;
using LPS.Domain.Common.Interfaces;
using System.IO;

namespace LPS.Infrastructure.LPSClients.PlaceHolderService
{
    public partial class PlaceholderResolverService(
        ISessionManager sessionManager,
        ICacheService<string> memoryCacheService,
        IVariableManager variableManager,
        IRuntimeOperationIdProvider runtimeOperationIdProvider,
        ILogger logger) : IPlaceholderResolverService
    {
        private readonly ISessionManager _sessionManager = sessionManager ?? throw new ArgumentNullException(nameof(sessionManager));
        private readonly IVariableManager _variableManager = variableManager ?? throw new ArgumentNullException(nameof(variableManager));
        private readonly ICacheService<string> _memoryCacheService = memoryCacheService ?? throw new ArgumentNullException(nameof(memoryCacheService));
        private readonly IRuntimeOperationIdProvider _runtimeOperationIdProvider= runtimeOperationIdProvider;
        readonly ILogger _logger= logger;
        public async Task<string> ResolvePlaceholdersAsync(string input, string sessionId, CancellationToken token)
        {
            if (string.IsNullOrWhiteSpace(input))
                return string.Empty;

            // Perform placeholder resolution
            string resolvedInput = await PerformResolution(input, sessionId, token);


            return resolvedInput.Trim();
        }

        private async Task<string> PerformResolution(string input, string sessionId, CancellationToken token)
        {
            StringBuilder result = new(input);
            int currentIndex = 0;

            while (currentIndex < result.Length)
            {
                // Handle escaped $$
                if (currentIndex + 1 < result.Length && result[currentIndex] == '$' && result[currentIndex + 1] == '$')
                {
                    result.Remove(currentIndex, 1); // Remove one $ to handle the escape
                    currentIndex++;
                    continue;
                }

                // Start of a placeholder
                if (result[currentIndex] == '$')
                {
                    int startIndex = currentIndex + 1;

                    // Find the end of the placeholder
                    int endIndex = startIndex;
                    bool insideParentheses = false;
                    bool insideSequareBracket = false;
                    int parenthesesBalance = 0;
                    int sequareBracketBalance = 0;

                    while (endIndex < result.Length)
                    {
                        char currentChar = result[endIndex];

                        // Handle parentheses for arguments
                        if (currentChar == '(')
                        {
                            insideParentheses = true;
                            parenthesesBalance++;
                        }
                        else if (currentChar == ')')
                        {
                            parenthesesBalance--;
                            if (parenthesesBalance == 0) 
                                insideParentheses = false;
                        }

                        if (currentChar == '[')
                        {
                            insideSequareBracket = true;
                            sequareBracketBalance++;
                        }
                        else if (currentChar == ']')
                        {
                            sequareBracketBalance--;
                            if (sequareBracketBalance == 0)
                                insideParentheses = false;
                        }

                        // Stop at the first valid delimiter outside parentheses
                        if (!insideParentheses && !insideSequareBracket && (char.IsWhiteSpace(currentChar) || currentChar == ',' || currentChar == '}' || currentChar == '"' || currentChar == '\'' || currentChar == '['))
                        {
                            break;
                        }

                        endIndex++;
                    }


                    // Extract the full placeholder, including arguments
                    string variableOrMethod = result.ToString(startIndex, endIndex - startIndex);
                    // Recursively resolve nested placeholders inside arguments
                    string resolvedPlaceholder = await ResolvePlaceholderAsync(variableOrMethod, sessionId, token);

                    // Replace the placeholder with the resolved value
                    result.Remove(currentIndex, endIndex - currentIndex);
                    result.Insert(currentIndex, resolvedPlaceholder);

                    // Move currentIndex past the replaced value
                    currentIndex += resolvedPlaceholder.Length;
                }
                else
                {
                    // Move to the next character if no placeholder is found
                    currentIndex++;
                }
            }

            return result.ToString();
        }
        private async Task<string> ResolvePlaceholderAsync(string variableOrMethodName, string sessionId, CancellationToken token)
        {
            string resolvedValue;
            if (IPlaceholderResolverService.IsSupportedPlaceHolderMethod($"${variableOrMethodName}"))
            {
                // Extract the function name and parameters
                int openParenIndex = variableOrMethodName.IndexOf('(');
                string functionName = variableOrMethodName.Substring(0, openParenIndex).Trim();
                string parameters = variableOrMethodName.Substring(openParenIndex + 1, variableOrMethodName.Length - openParenIndex - 2).Trim();
                // Resolve based on function name
                resolvedValue = functionName.ToLowerInvariant() switch
                {
                    "random" => await GenerateRandomStringAsync(parameters, sessionId, token), // Handle Random function
                    "randomnumber" => await GenerateRandomNumber(parameters, sessionId, token), // Handle RandomNumber function
                    "timestamp" => await GenerateTimestampAsync(parameters, sessionId, token), // Handle Timestamp function
                    "guid" => Guid.NewGuid().ToString(), // Handle Guid function
                    "base64encode" => await Base64EncodeAsync(parameters, sessionId, token), // Handle Base64Encode function
                    "hash" => await GenerateHashAsync(parameters, sessionId, token), // Handle Hash function
                    "read" => await ReadFileAsync(parameters, sessionId, token), // Handle ReadFile function
                    _ => throw new InvalidOperationException($"Unknown function: {functionName}") // Handle unknown functions
                };
            }
            else
            {
                // Treat as a plain variable
                resolvedValue = await ResolveVariableAsync(variableOrMethodName, sessionId, token);
            }
            return resolvedValue;
        }
        private async Task<string> ResolveVariableAsync(string placeholder, string sessionId, CancellationToken token)
        {
            if (_memoryCacheService.TryGetItem(placeholder, out string cachedResult))
            {
                return cachedResult;
            }

            // Check if the placeholder contains a path
            string variableName = placeholder;
            string path = null;
            string resolvedPlaceHolder;
            TimeSpan cacheDuration;
            bool isEmptyPlaceHolder = string.IsNullOrEmpty(placeholder);
            bool isMethod =  !isEmptyPlaceHolder  && (placeholder.Contains("(") && placeholder.Contains(")"));
            bool hasPath = (placeholder.Contains(".") || placeholder.Contains("/") || (placeholder.Contains("[") && placeholder.Contains("]")));
            if (!isEmptyPlaceHolder && !isMethod && hasPath) // Methods do not allow path so ignore the . and / chars
            {
                // Split the variable name and path
                var splitIndex = placeholder.IndexOfAny(['.', '/', '[']);
                variableName = placeholder.Substring(0, splitIndex);
                path = placeholder.Substring(splitIndex); // Extract path, including delimiters
            }

            // Get the variable holder from the session or global manager
            var variableHolder = await _sessionManager.GetResponseAsync(sessionId, variableName, token)
                                  ?? await _variableManager.GetVariableAsync(variableName, token);

            if (variableHolder == null)
            {
                await _logger.LogAsync(
                    _runtimeOperationIdProvider.OperationId,
                    $"The variable '{variableName}' was not found; it either does not exist or has not been defined yet. The variable will not be resolved.",
                    LPSLoggingLevel.Warning,
                    token
                );
                return $"${variableName}"; // Return unresolved placeholder
            }

            cacheDuration = variableHolder.IsGlobal ? TimeSpan.MaxValue : TimeSpan.FromSeconds(30);

            // Handle paths for structured formats
            if (!string.IsNullOrEmpty(path))
            {
                if (path.StartsWith(".") || path.StartsWith("[") && variableHolder.Format == MimeType.ApplicationJson)
                {
                    // JSON path extraction
                    resolvedPlaceHolder = variableHolder.ExtractJsonValue(path);
                }
                else if (path.StartsWith("/") &&
                         (variableHolder.Format == MimeType.ApplicationXml ||
                          variableHolder.Format == MimeType.TextXml ||
                          variableHolder.Format == MimeType.RawXml))
                {
                    // XML path extraction
                    resolvedPlaceHolder = variableHolder.ExtractXmlValue(path);
                }
                else if (path.StartsWith("[") && variableHolder.Format == MimeType.TextCsv)
                {
                    resolvedPlaceHolder = variableHolder.ExtractCsvValue(path);
                }
                else
                {
                    throw new InvalidOperationException($"Unsupported path '{path}' for variable '{variableName}' with format '{variableHolder.Format}'.");
                }
            }
            else
            {
                // If no path, return the raw response with regex applied
                resolvedPlaceHolder = variableHolder.ExtractValueWithRegex();
            }

            await _memoryCacheService.SetItemAsync(placeholder, resolvedPlaceHolder, cacheDuration);
            return resolvedPlaceHolder;
        }

        // Helper methods for dynamic functions
        private async Task<string> GenerateRandomStringAsync(string parameters, string sessionId, CancellationToken token)
        {
            // Default length
            int length = await ExtractValueFromParametersAsync(parameters, "length", 10, sessionId, token);

            // Generate the random string
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
            var random = new Random();
            return new string(Enumerable.Repeat(chars, length).Select(s => s[random.Next(s.Length)]).ToArray());
        }

        private async Task<string> GenerateRandomNumber(string parameters, string sessionId, CancellationToken token)
        {
            // Default range
            int min = await ExtractValueFromParametersAsync(parameters, "min", 1, sessionId, token);
            int max = await ExtractValueFromParametersAsync(parameters, "max", 100, sessionId, token);

            // Generate the random number
            var random = new Random();
            return random.Next(min, max + 1).ToString();
        }

        private async Task<string> GenerateTimestampAsync(string parameters, string sessionId, CancellationToken token)
        {
            string format = await ExtractStringFromParametersAsync(parameters, "format", "yyyy-MM-ddTHH:mm:ss", sessionId, token);
            return DateTime.UtcNow.ToString(format);
        }

        private async Task<string> Base64EncodeAsync(string parameters, string sessionId, CancellationToken token)
        {
            string value = await ExtractStringFromParametersAsync(parameters, "value", string.Empty, sessionId, token);
            return Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(value));
        }

        private async Task<string> GenerateHashAsync(string parameters, string sessionId, CancellationToken token)
        {
            string value = await ExtractStringFromParametersAsync(parameters, "value", string.Empty, sessionId, token);
            string algorithm = await ExtractStringFromParametersAsync(parameters, "algorithm", "SHA256", sessionId, token);

            using var hasher = algorithm switch
            {
                "MD5" => System.Security.Cryptography.MD5.Create(),
                "SHA256" => System.Security.Cryptography.SHA256.Create(),
                "SHA1" => (System.Security.Cryptography.HashAlgorithm)System.Security.Cryptography.SHA1.Create(),
                _ => throw new InvalidOperationException($"Unsupported hash algorithm: {algorithm}")
            };

            byte[] hash = hasher.ComputeHash(System.Text.Encoding.UTF8.GetBytes(value));
            return BitConverter.ToString(hash).Replace("-", "").ToLower();
        }
        private async Task<string> ReadFileAsync(string parameters, string sessionId, CancellationToken token)
        {
            // Extract the file path from parameters
            string filePath = await ExtractStringFromParametersAsync(parameters, "path", string.Empty, sessionId, token);
            if (string.IsNullOrWhiteSpace(filePath))
            {
                throw new ArgumentException("File path cannot be null or empty.", nameof(parameters));
            }

            // Validate the file path (optional: add security checks for allowed directories)
            if (!System.IO.File.Exists(filePath))
            {
                await _logger.LogAsync(_runtimeOperationIdProvider.OperationId, $"File '{filePath}' does not exist.", LPSLoggingLevel.Warning, token);
                return string.Empty; // Return an empty string for non-existent files
            }

            try
            {
                // Read file content
                using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                using var reader = new StreamReader(fileStream, Encoding.UTF8);

                string content = await reader.ReadToEndAsync();

                // Log the successful read operation
                await _logger.LogAsync(_runtimeOperationIdProvider.OperationId, $"Successfully read content from file '{filePath}'.", LPSLoggingLevel.Information, token);
                return content;
            }
            catch (Exception ex)
            {
                await _logger.LogAsync(_runtimeOperationIdProvider.OperationId, $"Error reading file '{filePath}': {ex.Message}", LPSLoggingLevel.Error, token);
                throw new InvalidOperationException($"Failed to read the file at '{filePath}'. See logs for details.", ex);
            }
        }

        // Helper methods for extracting values from placeholders
        private async Task<int> ExtractValueFromParametersAsync(string parameters, string key, int defaultValue, string sessionId, CancellationToken token)
        {
            if (string.IsNullOrEmpty(parameters))
                return defaultValue;

            // Parse "key=value" pairs
            var keyValuePairs = parameters.Split(',');
            foreach (var pair in keyValuePairs)
            {
                var parts = pair.Split('=', 2);
                if (parts.Length == 2 && parts[0].Trim() == key)
                {
                    // Resolve any placeholders in the value
                    string resolvedValue = await PerformResolution(parts[1].Trim(), sessionId, token);

                    // Attempt to parse the resolved value
                    return int.TryParse(resolvedValue, out int result) ? result : defaultValue;
                }
            }

            return defaultValue; // Return default if key is not found
        }

        private async Task<string> ExtractStringFromParametersAsync(string parameters, string key, string defaultValue, string sessionId, CancellationToken token)
        {
            if (string.IsNullOrEmpty(parameters))
                return defaultValue;

            // Parse "key=value" pairs
            var keyValuePairs = parameters.Split(',');
            foreach (var pair in keyValuePairs)
            {
                var parts = pair.Split('=', 2);
                if (parts.Length == 2 && parts[0].Trim() == key)
                {
                    // Resolve any placeholders in the value
                    return await PerformResolution(parts[1].Trim(), sessionId, token);
                }
            }

            return defaultValue; // Return default if key is not found
        }
    }
}
