using Newtonsoft.Json.Linq;
using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using LPS.Domain.Common;
using LPS.Domain.Common.Interfaces;
using LPS.Infrastructure.VariableServices.GlobalVariableManager;

namespace LPS.Infrastructure.PlaceHolderService.Methods
{
    public sealed class LengthMethod : MethodBase
    {
        public LengthMethod(ParameterExtractorService p, ILogger l, IRuntimeOperationIdProvider op, IVariableManager v, Lazy<IPlaceholderResolverService> r)
            : base(p, l, op, v, r)
        {
        }

        public override string Name => "length";

        public override async Task<string> ExecuteAsync(string parameters, string sessionId, CancellationToken token)
        {
            string variableName = string.Empty;

            try
            {
                var targetExpression = await _params.ExtractStringAsync(parameters, "from", string.Empty, sessionId, token);
                if (string.IsNullOrWhiteSpace(targetExpression))
                {
                    targetExpression = await _params.ExtractStringAsync(parameters, "value", string.Empty, sessionId, token);
                }
                if (string.IsNullOrWhiteSpace(targetExpression))
                {
                    targetExpression = await _params.ExtractStringAsync(parameters, "path", string.Empty, sessionId, token);
                }
                if (string.IsNullOrWhiteSpace(targetExpression))
                {
                    targetExpression = ExtractPositionalParameter(parameters);
                }

                variableName = await _params.ExtractStringAsync(parameters, "variable", string.Empty, sessionId, token);

                if (string.IsNullOrWhiteSpace(targetExpression))
                {
                    await _logger.LogAsync(_op.OperationId, "length failed. No source expression was provided.", LPSLoggingLevel.Warning, token);
                    await StoreVariableIfNeededAsync(variableName, "0", token);
                    return "0";
                }

                var resolvedValue = await _resolver.Value.ResolvePlaceholdersAsync<string>(targetExpression, sessionId, token);
                if (string.IsNullOrWhiteSpace(resolvedValue))
                {
                    await _logger.LogAsync(_op.OperationId, "length failed. The resolved value was empty.", LPSLoggingLevel.Warning, token);
                    await StoreVariableIfNeededAsync(variableName, "0", token);
                    return "0";
                }

                if (!TryGetJsonArrayLength(resolvedValue, out var length))
                {
                    await _logger.LogAsync(_op.OperationId, "length failed. The resolved value is not a JSON array.", LPSLoggingLevel.Warning, token);
                    await StoreVariableIfNeededAsync(variableName, "0", token);
                    return "0";
                }

                var result = length.ToString(CultureInfo.InvariantCulture);
                await StoreVariableIfNeededAsync(variableName, result, token);
                return result;
            }
            catch (Exception ex)
            {
                await _logger.LogAsync(_op.OperationId, $"length failed. {ex}", LPSLoggingLevel.Error, token);
                await StoreVariableIfNeededAsync(variableName, "0", token);
                return "0";
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

        private static bool TryGetJsonArrayLength(string value, out int length)
        {
            length = 0;

            try
            {
                var token = JToken.Parse(value);
                if (token is JArray array)
                {
                    length = array.Count;
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
