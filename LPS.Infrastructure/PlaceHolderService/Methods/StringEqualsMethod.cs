using System;
using System.Threading;
using System.Threading.Tasks;
using LPS.Domain.Common;
using LPS.Domain.Common.Interfaces;
using LPS.Infrastructure.VariableServices.GlobalVariableManager;

namespace LPS.Infrastructure.PlaceHolderService.Methods
{
    /// <summary>
    /// $stringequals(a=..., b=..., ignoreCase=false, variable=name) — returns "true" when the two
    /// resolved strings are equal, else "false". Set ignoreCase=true for case-insensitive comparison.
    /// </summary>
    public sealed class StringEqualsMethod : MethodBase
    {
        public StringEqualsMethod(ParameterExtractorService p, ILogger l, IRuntimeOperationIdProvider op, IVariableManager v, Lazy<IPlaceholderResolverService> r)
            : base(p, l, op, v, r)
        {
        }

        public override string Name => "stringequals";

        public override async Task<string> ExecuteAsync(string parameters, string sessionId, CancellationToken token)
        {
            string variableName = string.Empty;

            try
            {
                variableName = await _params.ExtractStringAsync(parameters, "variable", string.Empty, sessionId, token);

                var a = await _params.ExtractStringAsync(parameters, "a", string.Empty, sessionId, token) ?? string.Empty;
                var b = await _params.ExtractStringAsync(parameters, "b", string.Empty, sessionId, token) ?? string.Empty;
                var ignoreCase = await _params.ExtractBoolAsync(parameters, "ignoreCase", false, sessionId, token);

                var comparison = ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
                var text = string.Equals(a, b, comparison) ? "true" : "false";

                await StoreVariableIfNeededAsync(variableName, text, token);
                return text;
            }
            catch (Exception ex)
            {
                await _logger.LogAsync(_op.OperationId, $"stringequals failed. {ex}", LPSLoggingLevel.Error, token);
                await StoreVariableIfNeededAsync(variableName, "false", token);
                return "false";
            }
        }
    }
}
