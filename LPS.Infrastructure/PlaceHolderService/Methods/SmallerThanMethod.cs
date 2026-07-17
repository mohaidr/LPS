using System;
using System.Threading;
using System.Threading.Tasks;
using LPS.Domain.Common;
using LPS.Domain.Common.Interfaces;
using LPS.Infrastructure.VariableServices.GlobalVariableManager;

namespace LPS.Infrastructure.PlaceHolderService.Methods
{
    /// <summary>$smallerthan(a=..., b=..., variable=name) — returns "true" when a &lt; b, else "false".</summary>
    public sealed class SmallerThanMethod : MethodBase
    {
        public SmallerThanMethod(ParameterExtractorService p, ILogger l, IRuntimeOperationIdProvider op, IVariableManager v, Lazy<IPlaceholderResolverService> r)
            : base(p, l, op, v, r)
        {
        }

        public override string Name => "smallerthan";

        public override async Task<string> ExecuteAsync(string parameters, string sessionId, CancellationToken token)
        {
            string variableName = string.Empty;

            try
            {
                variableName = await _params.ExtractStringAsync(parameters, "variable", string.Empty, sessionId, token);
                var numberVariable = await _params.ExtractStringAsync(parameters, "numberVariable", string.Empty, sessionId, token);

                var a = await ResolveNumberParamAsync(parameters, "a", sessionId, token);
                var b = await ResolveNumberParamAsync(parameters, "b", sessionId, token);
                if (a is null || b is null)
                {
                    await _logger.LogAsync(_op.OperationId, "smallerthan failed. Both numeric operands 'a' and 'b' are required.", LPSLoggingLevel.Warning, token);
                    await StoreStringVariableAsync(variableName, "false", token);
                    return "false";
                }

                var text = (a.Value < b.Value) ? "true" : "false";
                await StoreStringVariableAsync(variableName, text, token);
                if (!string.IsNullOrWhiteSpace(numberVariable))
                    await StoreNumberResultAsync(numberVariable, Math.Min(a.Value, b.Value), string.Empty, token);
                return text;
            }
            catch (Exception ex)
            {
                await _logger.LogAsync(_op.OperationId, $"smallerthan failed. {ex}", LPSLoggingLevel.Error, token);
                await StoreStringVariableAsync(variableName, "false", token);
                return "false";
            }
        }
    }
}
