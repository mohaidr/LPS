using Newtonsoft.Json.Linq;
using System;
using System.Threading;
using System.Threading.Tasks;
using LPS.Domain.Common;
using LPS.Domain.Common.Interfaces;
using LPS.Infrastructure.VariableServices.GlobalVariableManager;

namespace LPS.Infrastructure.PlaceHolderService.Methods
{
    public sealed class RandomItemMethod : MethodBase
    {
        public RandomItemMethod(ParameterExtractorService p, ILogger l, IRuntimeOperationIdProvider op, IVariableManager v, Lazy<IPlaceholderResolverService> r)
            : base(p, l, op, v, r)
        {
        }

        public override string Name => "randomItem";

        public override async Task<string> ExecuteAsync(string parameters, string sessionId, CancellationToken token)
        {
            string variableName = string.Empty;

            try
            {
                var targetExpression = await _params.ExtractStringAsync(parameters, "source", string.Empty, sessionId, token);
                if (string.IsNullOrWhiteSpace(targetExpression))
                {
                    targetExpression = ExtractPositionalParameter(parameters);
                }

                variableName = await _params.ExtractStringAsync(parameters, "variable", string.Empty, sessionId, token);

                if (string.IsNullOrWhiteSpace(targetExpression))
                {
                    await _logger.LogAsync(_op.OperationId, "randomItem failed. No source expression was provided.", LPSLoggingLevel.Warning, token);
                    await StoreVariableIfNeededAsync(variableName, string.Empty, token);
                    return string.Empty;
                }

                var resolvedValue = await _resolver.Value.ResolvePlaceholdersAsync<string>(targetExpression, sessionId, token);
                if (string.IsNullOrWhiteSpace(resolvedValue))
                {
                    await _logger.LogAsync(_op.OperationId, "randomItem failed. The resolved value was empty.", LPSLoggingLevel.Warning, token);
                    await StoreVariableIfNeededAsync(variableName, string.Empty, token);
                    return string.Empty;
                }

                if (!TryGetJsonArray(resolvedValue, out var array) || array.Count == 0)
                {
                    await _logger.LogAsync(_op.OperationId, "randomItem failed. The resolved value is not a JSON array or the array is empty.", LPSLoggingLevel.Warning, token);
                    await StoreVariableIfNeededAsync(variableName, string.Empty, token);
                    return string.Empty;
                }

                var selected = array[Random.Shared.Next(array.Count)];
                var result = selected.ToString(Newtonsoft.Json.Formatting.None);

                await StoreVariableIfNeededAsync(variableName, result, token);
                return result;
            }
            catch (Exception ex)
            {
                await _logger.LogAsync(_op.OperationId, $"randomItem failed. {ex}", LPSLoggingLevel.Error, token);
                await StoreVariableIfNeededAsync(variableName, string.Empty, token);
                return string.Empty;
            }
        }

        private static string ExtractPositionalParameter(string parameters)
        {
            if (string.IsNullOrWhiteSpace(parameters))
            {
                return string.Empty;
            }

            foreach (var part in parameters.Split(','))
            {
                var trimmed = part.Trim();
                if (!string.IsNullOrWhiteSpace(trimmed) && !trimmed.Contains('='))
                {
                    return trimmed;
                }
            }

            return string.Empty;
        }

        private static bool TryGetJsonArray(string value, out JArray array)
        {
            array = new JArray();

            try
            {
                var token = JToken.Parse(value);
                if (token is JArray parsedArray)
                {
                    array = parsedArray;
                    return true;
                }
            }
            catch
            {
            }

            return false;
        }
    }
}
