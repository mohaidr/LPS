using LPS.Domain.Common;
using LPS.Infrastructure.LPSClients.SessionManager;
using LPS.Infrastructure.Caching;
using System;
using System.Linq;
using System.Text;
using LPS.Infrastructure.LPSClients.GlobalVariableManager;

namespace LPS.Infrastructure.LPSClients.PlaceHolderService
{
    public partial class PlaceholderResolverService(ISessionManager sessionManager,
        ICacheService<string> memoryCacheService,
        IVariableManager variableManager) : IPlaceholderResolverService
    {
        private readonly ISessionManager _sessionManager = sessionManager ?? throw new ArgumentNullException(nameof(sessionManager));
        private readonly IVariableManager _variableManager = variableManager ?? throw new ArgumentNullException(nameof(variableManager));
        private readonly ICacheService<string> _memoryCacheService = memoryCacheService ?? throw new ArgumentNullException(nameof(memoryCacheService));

        public string ResolvePlaceholders(string input, string identifier)
        {
            if (string.IsNullOrWhiteSpace(input))
                return string.Empty;

            // Use a cache key unique to the input and clientId
            string cacheKey = $"Placeholder_{identifier}_{input.GetHashCode()}";

            // Check if the result is already cached
            if (_memoryCacheService.TryGetItem(cacheKey, out string cachedResult))
            {
                return cachedResult;
            }

            // Perform placeholder resolution
            string resolvedInput = PerformResolution(input, identifier);

            // Cache the resolved result
            _memoryCacheService.SetItemAsync(cacheKey, resolvedInput).Wait();

            return resolvedInput;
        }

        private string PerformResolution(string input, string sessionId)
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
                    int parenthesesBalance = 0;

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

                        // Stop at the first valid delimiter outside parentheses
                        if (!insideParentheses && (char.IsWhiteSpace(currentChar) || currentChar == ',' || currentChar == '}' || currentChar == '"' || currentChar == '\''))
                        {
                            break;
                        }

                        endIndex++;
                    }

                    // Extract the full placeholder, including arguments
                    string rawInput = result.ToString(startIndex, endIndex - startIndex);
                    // Recursively resolve nested placeholders inside arguments
                    string resolvedPlaceholder = ResolvePlaceholder(rawInput, sessionId);

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

        private string ResolvePlaceholder(string placeholder, string sessionId)
        {
            // Check if the placeholder is a function (contains parentheses)
            bool isFunction = placeholder.Contains('(') && placeholder.EndsWith(')');

            if (isFunction)
            {
                // Extract the function name and parameters
                int openParenIndex = placeholder.IndexOf('(');
                string functionName = placeholder.Substring(0, openParenIndex).Trim();
                string parameters = placeholder.Substring(openParenIndex + 1, placeholder.Length - openParenIndex - 2).Trim();
                // Resolve based on function name
                return functionName switch
                {
                    "Random" => GenerateRandomString(parameters, sessionId), // Handle Random function
                    "RandomNumber" => GenerateRandomNumber(parameters, sessionId), // Handle RandomNumber function
                    "Timestamp" => GenerateTimestamp(parameters, sessionId), // Handle Timestamp function
                    "Guid" => Guid.NewGuid().ToString(), // Handle Guid function
                    "Base64Encode" => Base64Encode(parameters, sessionId), // Handle Base64Encode function
                    "Hash" => GenerateHash(parameters, sessionId), // Handle Hash function
                    "CustomVariable" => ResolveCustomVariable(parameters, sessionId), // Handle CustomVariable function
                    _ => throw new InvalidOperationException($"Unknown function: {functionName}") // Handle unknown functions
                };
            }
            else
            {
                // Treat as a plain variable
                return ResolveVariable(placeholder, sessionId);
            }
        }

        private string ResolveVariable(string variableName, string sessionId)
        {
            var response = _sessionManager.GetResponse(sessionId, variableName) ?? _variableManager.GetVariable(variableName);
            if (response == null)
            {
                throw new InvalidOperationException($"Variable '{variableName}' not found in session.");
            }
            return response.ApplyRegexAndReturn();
        }

        // Helper methods for dynamic functions
        private string GenerateRandomString(string parameters, string sessionId)
        {
            // Default length
            int length = ExtractValueFromParameters(parameters, "length", 10, sessionId);

            // Generate the random string
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
            var random = new Random();
            return new string(Enumerable.Repeat(chars, length).Select(s => s[random.Next(s.Length)]).ToArray());
        }

        private string GenerateRandomNumber(string parameters, string sessionId)
        {
            // Default range
            int min = ExtractValueFromParameters(parameters, "min", 1, sessionId);
            int max = ExtractValueFromParameters(parameters, "max", 100, sessionId);

            // Generate the random number
            var random = new Random();
            return random.Next(min, max + 1).ToString();
        }

        private string GenerateTimestamp(string parameters, string sessionId)
        {
            string format = ExtractStringFromParameters(parameters, "format", "yyyy-MM-ddTHH:mm:ss", sessionId);
            return DateTime.UtcNow.ToString(format);
        }

        private string Base64Encode(string parameters, string sessionId)
        {
            string value = ExtractStringFromParameters(parameters, "value", string.Empty, sessionId);
            return Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(value));
        }

        private string GenerateHash(string parameters, string sessionId)
        {
            string value = ExtractStringFromParameters(parameters, "value", string.Empty, sessionId);
            string algorithm = ExtractStringFromParameters(parameters, "algorithm", "SHA256", sessionId);

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

        private string ResolveCustomVariable(string parameters, string sessionId)
        {
            string name = ExtractStringFromParameters(parameters, "name", string.Empty, sessionId);
            var variable = _variableManager.GetVariable(name);
            return variable != null ? variable.ToString() : string.Empty;
        }

        // Helper methods for extracting values from placeholders
        private int ExtractValueFromParameters(string parameters, string key, int defaultValue, string sessionId)
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
                    string resolvedValue = PerformResolution(parts[1].Trim(), sessionId);

                    // Attempt to parse the resolved value
                    return int.TryParse(resolvedValue, out int result) ? result : defaultValue;
                }
            }

            return defaultValue; // Return default if key is not found
        }

        private string ExtractStringFromParameters(string parameters, string key, string defaultValue, string sessionId)
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
                    return PerformResolution(parts[1].Trim(), sessionId);
                }
            }

            return defaultValue; // Return default if key is not found
        }
    }
}
