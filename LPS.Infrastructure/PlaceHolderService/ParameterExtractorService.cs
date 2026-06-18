using LPS.Domain.Common.Interfaces;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using LPS.Infrastructure.Common.Expressions;

namespace LPS.Infrastructure.PlaceHolderService
{
    public class ParameterExtractorService
    {
        private static readonly Regex NumericExpressionRegex = new(@"^[0-9\s\.\+\-\*\/\(\)]+$", RegexOptions.Compiled);

        private readonly Lazy<IPlaceholderResolverService> _resolver;
        private readonly IRuntimeOperationIdProvider _runtimeOperationIdProvider;
        private readonly ILogger _logger;

        public ParameterExtractorService(
            Lazy<IPlaceholderResolverService> resolver,
            IRuntimeOperationIdProvider runtimeOperationIdProvider,
            ILogger logger)
        {
            _resolver = resolver;
            _runtimeOperationIdProvider = runtimeOperationIdProvider ?? throw new ArgumentNullException(nameof(runtimeOperationIdProvider));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        }


        public async Task<int> ExtractNumberAsync(string parameters, string key, int defaultValue, string sessionId, CancellationToken token)
        {
            if (string.IsNullOrEmpty(parameters))
            {
                return defaultValue;
            }

            var keyValuePairs = SplitParametersAware(parameters);
            foreach (var pair in keyValuePairs)
            {
                var parts = pair.Split('=', 2);
                if (parts.Length == 2 && parts[0].Trim() == key)
                {
                    var resolvedValue = await _resolver.Value.ResolvePlaceholdersAsync<string>(parts[1].Trim(), sessionId, token);

                    if (int.TryParse(resolvedValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedValue))
                    {
                        return parsedValue;
                    }

                    if (TryEvaluateNumericExpression(resolvedValue, out var evaluatedValue))
                    {
                        return evaluatedValue;
                    }

                    await _logger.LogAsync(_runtimeOperationIdProvider.OperationId,
                        $"Failed to parse numeric parameter '{key}' from value '{resolvedValue}'. Falling back to default '{defaultValue}'.",
                        LPSLoggingLevel.Warning,
                        token);

                    return defaultValue;
                }
            }

            return defaultValue;
        }

        private static bool TryEvaluateNumericExpression(string value, out int result)
        {
            return ArithmeticExpressionEvaluator.TryEvaluateToInt(value, out result);
        }

        public async Task<string> ExtractStringAsync(string parameters, string key, string defaultValue, string sessionId, CancellationToken token)
        {
            if (string.IsNullOrEmpty(parameters))
                return defaultValue;

            var keyValuePairs = SplitParametersAware(parameters);
            foreach (var pair in keyValuePairs)
            {
                var parts = pair.Split('=', 2);
                if (parts.Length == 2 && parts[0].Trim() == key)
                {
                    return await _resolver.Value.ResolvePlaceholdersAsync<string>(parts[1].Trim(), sessionId, token);
                }
            }

            return defaultValue;
        }


        public async Task<bool> ExtractBoolAsync(string parameters, string key, bool defaultValue, string sessionId, CancellationToken token)
        {
            if (string.IsNullOrEmpty(parameters))
                return defaultValue;

            var keyValuePairs = SplitParametersAware(parameters);

            foreach (var pair in keyValuePairs)
            {
                var parts = pair.Split('=', 2);
                if (parts.Length == 2 && parts[0].Trim() == key)
                {
                    return await _resolver.Value.ResolvePlaceholdersAsync<bool>(parts[1].Trim(), sessionId, token);
                }
            }

            return defaultValue;
        }

        private static List<string> SplitParametersAware(string parameters)
        {
            var result = new List<string>();
            var current = new StringBuilder();
            int braceDepth = 0;
            int parenthesesDepth = 0;
            int bracketDepth = 0;

            foreach (char c in parameters)
            {
                if (c == '{')
                {
                    braceDepth++;
                    current.Append(c);
                }
                else if (c == '}')
                {
                    braceDepth--;
                    current.Append(c);
                }
                else if (c == '(')
                {
                    parenthesesDepth++;
                    current.Append(c);
                }
                else if (c == ')')
                {
                    parenthesesDepth--;
                    current.Append(c);
                }
                else if (c == '[')
                {
                    bracketDepth++;
                    current.Append(c);
                }
                else if (c == ']')
                {
                    bracketDepth--;
                    current.Append(c);
                }
                else if (c == ',' && braceDepth == 0 && parenthesesDepth == 0 && bracketDepth == 0)
                {
                    result.Add(current.ToString());
                    current.Clear();
                }
                else
                {
                    current.Append(c);
                }
            }

            if (current.Length > 0)
            {
                result.Add(current.ToString());
            }

            return result;
        }
    }

}
