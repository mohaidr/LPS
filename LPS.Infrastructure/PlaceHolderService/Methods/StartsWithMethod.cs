using System;
using System.Threading;
using System.Threading.Tasks;
using LPS.Domain.Common;
using LPS.Domain.Common.Interfaces;
using LPS.Infrastructure.VariableServices.GlobalVariableManager;

namespace LPS.Infrastructure.PlaceHolderService.Methods
{
    public sealed class StartsWithMethod : MethodBase
    {
        public StartsWithMethod(ParameterExtractorService p, ILogger l, IRuntimeOperationIdProvider op, IVariableManager v, Lazy<IPlaceholderResolverService> r)
            : base(p, l, op, v, r)
        {
        }

        public override string Name => "startswith";

        public override async Task<string> ExecuteAsync(string parameters, string sessionId, CancellationToken token)
        {
            string variableName = string.Empty;

            try
            {
                var source = await _params.ExtractStringAsync(parameters, "source", string.Empty, sessionId, token);
                if (string.IsNullOrWhiteSpace(source))
                {
                    source = ExtractPositionalParameter(parameters);
                }

                var value = await _params.ExtractStringAsync(parameters, "value", string.Empty, sessionId, token);
                variableName = await _params.ExtractStringAsync(parameters, "variable", string.Empty, sessionId, token);
                var ignoreCase = await _params.ExtractBoolAsync(parameters, "ignoreCase", false, sessionId, token);

                if (string.IsNullOrWhiteSpace(source) || string.IsNullOrWhiteSpace(value))
                {
                    await _logger.LogAsync(_op.OperationId, "startswith failed. Source and value are required.", LPSLoggingLevel.Warning, token);
                    await StoreVariableIfNeededAsync(variableName, "false", token);
                    return "false";
                }

                var resolvedSource = await _resolver.Value.ResolvePlaceholdersAsync<string>(source, sessionId, token) ?? string.Empty;
                var resolvedValue = await _resolver.Value.ResolvePlaceholdersAsync<string>(value, sessionId, token) ?? string.Empty;

                var comparison = ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
                var result = resolvedSource.StartsWith(resolvedValue, comparison);
                var resultText = result.ToString().ToLowerInvariant();

                await StoreVariableIfNeededAsync(variableName, resultText, token);
                return resultText;
            }
            catch (Exception ex)
            {
                await _logger.LogAsync(_op.OperationId, $"startswith failed. {ex}", LPSLoggingLevel.Error, token);
                await StoreVariableIfNeededAsync(variableName, "false", token);
                return "false";
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
    }
}