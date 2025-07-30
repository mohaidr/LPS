using LPS.Domain.Common;
using LPS.Infrastructure.LPSClients.SessionManager;
using LPS.Infrastructure.Caching;
using LPS.Infrastructure.LPSClients.GlobalVariableManager;
using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using LPS.Domain.Common.Interfaces;
using System.IO;
using LPS.Infrastructure.Common;
using LPS.Infrastructure.Logger;
using LPS.Infrastructure.LPSClients.CachService;

namespace LPS.Infrastructure.LPSClients.PlaceHolderService
{
    public partial class PlaceholderResolverService : IPlaceholderResolverService
    {
        private readonly IRuntimeOperationIdProvider _runtimeOperationIdProvider;
        private readonly ILogger _logger;
        private readonly PlaceholderProcessor _processor;
        public PlaceholderResolverService(
            ISessionManager sessionManager,
            ICacheService<string> memoryCacheService,
            IVariableManager variableManager,
            IRuntimeOperationIdProvider runtimeOperationIdProvider,
            ILogger logger)
        {
            _runtimeOperationIdProvider = runtimeOperationIdProvider ?? throw new ArgumentNullException(nameof(runtimeOperationIdProvider));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            var paramExtractionService = new ParameterExtractorService(
                this,
                _runtimeOperationIdProvider,
                _logger);
            _processor = new PlaceholderProcessor(
                paramExtractionService,
                sessionManager,
                memoryCacheService,
                variableManager,
                this,
                runtimeOperationIdProvider,
                logger);
        }

        public async Task<T> ResolvePlaceholdersAsync<T>(string input, string sessionId, CancellationToken token)
        {
            if (string.IsNullOrWhiteSpace(input))
                return default; // Return default value of the specified type

            string resolvedValue = await ParseAsync(input, sessionId, token);

            try
            {
                // Handle nullable types
                var targetType = Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T);

                if (targetType.IsEnum)
                {
                    // Attempt to parse the resolved value into the enum
                    if (Enum.TryParse(targetType, resolvedValue, true, out var enumValue))
                    {
                        return (T)enumValue;
                    }
                    else
                    {
                        throw new InvalidOperationException($"Failed to convert placeholder value '{resolvedValue}' to enum type {targetType}.");
                    }
                }

                // Handle TimeSpan explicitly
                if (targetType == typeof(TimeSpan))
                {
                    if (TimeSpan.TryParse(resolvedValue, out var timeSpanValue))
                        return (T)(object)timeSpanValue;

                    throw new InvalidOperationException($"Failed to parse '{resolvedValue}' as TimeSpan.");
                }
                // For non-enum types, handle nullable and regular types
                var convertedValue = string.IsNullOrEmpty(resolvedValue)
                    ? default
                    : Convert.ChangeType(resolvedValue, targetType);

                return (T)convertedValue;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to convert placeholder value to type {typeof(T)}.", ex);
            }
        }

        private async Task<string> ParseAsync(string input, string sessionId, CancellationToken token)
        {
            if (string.IsNullOrWhiteSpace(input) || !input.Contains('$'))
                return input;

            StringBuilder result = new(input.Trim());
            int currentIndex = 0;

            while (currentIndex < result.Length)
            {
                if (currentIndex + 1 < result.Length && result[currentIndex] == '$' && result[currentIndex + 1] == '$')
                {
                    result.Remove(currentIndex, 1);
                    currentIndex++;
                    continue;
                }

                if (currentIndex + 1 < result.Length && result[currentIndex] == '$')
                {
                    if (currentIndex + 2 < result.Length && result[currentIndex + 1] == '{')
                    {
                        // Handle ${variable} syntax
                        int startIndex = currentIndex + 2;
                        int stopperIndex = FindStopperIndex(result, startIndex); // the stopper will always be }

                        string placeholder = result.ToString(startIndex, stopperIndex - startIndex); // Exclude closing '}'
                        string resolvedValue = await _processor.ResolvePlaceholderAsync(placeholder, sessionId, token);

                        result.Remove(currentIndex, stopperIndex - currentIndex + 1); // to remove }
                        result.Insert(currentIndex, resolvedValue);
                        currentIndex += resolvedValue.Length;
                    }
                    else
                    {
                        int startIndex = currentIndex + 1;
                        int stopperIndex = FindStopperIndex(result, startIndex); // Stoppers like / ; , ] } etc., indicate the end of a variable. For example, in $x,$y, the ',' acts as a stopper, signaling that $x is a complete placeholder to resolve, so $x,$y should be treated as two separate variables.
                        string placeholder = result.ToString(startIndex, stopperIndex - startIndex);
                        string resolvedValue = await _processor.ResolvePlaceholderAsync(placeholder, sessionId, token);

                        result.Remove(currentIndex, stopperIndex - currentIndex);
                        result.Insert(currentIndex, resolvedValue);
                        currentIndex += resolvedValue.Length;
                    }
                }
                else
                {
                    currentIndex++;
                }
            }

            return result.ToString();
        }

        private static int FindStopperIndex(StringBuilder result, int startIndex)
        {
            int endIndex = startIndex;
            bool insideParentheses;
            bool insideSquareBracket;
            int parenthesesBalance = 0;
            int squareBracketBalance = 0;

            // Check for ${variable} syntax
            if (startIndex > 1 && result[startIndex - 2] == '$' && result[startIndex - 1] == '{')
            {
                // Look for the matching closing '}'
                while (endIndex < result.Length)
                {
                    if (result[endIndex] == '}')
                    {
                        return endIndex;
                    }
                    endIndex++;
                }

                throw new InvalidOperationException("Unmatched '{' in variable.");
            }


            char[] pathChars = ['.', '/', '[', ']'];
            bool isMethod = false;
            char lastChar = ' ';
            char currentChar = ' ';
            while (endIndex < result.Length)
            {
                currentChar = result[endIndex];
                if (currentChar == '(') { parenthesesBalance++; isMethod = true; }
                if (currentChar == ')') parenthesesBalance--;
                if (currentChar == '[') { squareBracketBalance++; };
                if (currentChar == ']') { squareBracketBalance--; };
                insideParentheses = parenthesesBalance > 0;
                insideSquareBracket = squareBracketBalance > 0;
                if ((!insideParentheses && !insideSquareBracket &&
                    !char.IsLetterOrDigit(currentChar) && !pathChars.Contains(currentChar))
                    || parenthesesBalance < 0
                    || squareBracketBalance < 0)
                {
                    break;
                }
                lastChar = currentChar;
                endIndex++;
            }

            /*
             For methods, the endIndex is increased to ensure the closing ')' is not excluded from the placeholder name.
             For variables, the stopper should not be part of the variable name. The caller method determines the placeholder name by subtracting the start index (the first letter after $) from the stopper index.
             For example, in $x_$y, the stopper index is 2, and the start index is 1. Subtracting 1 from 2 gives the variable name length (1), allowing us to extract the variable name by slicing from the start index for the calculated length.
            */
            if (isMethod && currentChar == ')' && parenthesesBalance == 0)
            {
                endIndex++;
            }
            // handle the case "[$x, $y]" so we do not take ] with $y
            if (squareBracketBalance != 0
                && (lastChar == '[' || lastChar == ']'))
                endIndex--;

            return endIndex;
        }

    }

}
