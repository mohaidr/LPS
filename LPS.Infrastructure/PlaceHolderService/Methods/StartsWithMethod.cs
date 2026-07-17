using System;
using System.Threading;
using System.Threading.Tasks;
using LPS.Domain.Common;
using LPS.Domain.Common.Interfaces;
using LPS.Infrastructure.LPSClients.SessionManager;
using LPS.Infrastructure.VariableServices.GlobalVariableManager;

namespace LPS.Infrastructure.PlaceHolderService.Methods
{
    public sealed class StartsWithMethod : MethodBase
    {
        public StartsWithMethod(ParameterExtractorService p, ILogger l, IRuntimeOperationIdProvider op, IVariableManager v, Lazy<IPlaceholderResolverService> r, ISessionManager sessionManager)
            : base(p, l, op, v, r, sessionManager)
        {
        }

        public override string Name => "startswith";

        public override async Task<string> ExecuteAsync(string parameters, string sessionId, CancellationToken token)
        {
            string variableName = string.Empty;
            bool isGlobal = false;

            try
            {
                // Extract RAW then resolve ONCE (previously ExtractStringAsync resolved and the trailing
                // ResolvePlaceholdersAsync resolved again — a double pass that could mangle values containing
                // a literal '$'). The positional fallback is already raw.
                var source = _params.ExtractRawString(parameters, "source", string.Empty);
                if (string.IsNullOrWhiteSpace(source))
                {
                    source = ExtractPositionalParameter(parameters);
                }

                var value = _params.ExtractRawString(parameters, "value", string.Empty);
                variableName = await _params.ExtractStringAsync(parameters, "variable", string.Empty, sessionId, token);
                var ignoreCase = await _params.ExtractBoolAsync(parameters, "ignoreCase", false, sessionId, token);
                isGlobal = await _params.ExtractBoolAsync(parameters, "isGlobal", false, sessionId, token);

                var resolvedSource = await _resolver.Value.ResolvePlaceholdersAsync<string>(source, sessionId, token) ?? string.Empty;
                var resolvedValue = await _resolver.Value.ResolvePlaceholdersAsync<string>(value, sessionId, token) ?? string.Empty;

                if (string.IsNullOrWhiteSpace(resolvedSource) || string.IsNullOrWhiteSpace(resolvedValue))
                {
                    await _logger.LogAsync(_op.OperationId, "startswith failed. Source and value are required.", LPSLoggingLevel.Warning, token);
                    await StoreStringVariableAsync(variableName, "false", token, sessionId, isGlobal);
                    return "false";
                }

                var comparison = ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
                var result = resolvedSource.StartsWith(resolvedValue, comparison);
                var resultText = result.ToString().ToLowerInvariant();

                await StoreStringVariableAsync(variableName, resultText, token, sessionId, isGlobal);
                return resultText;
            }
            catch (Exception ex)
            {
                await _logger.LogAsync(_op.OperationId, $"startswith failed. {ex}", LPSLoggingLevel.Error, token);
                await StoreStringVariableAsync(variableName, "false", token, sessionId, isGlobal);
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