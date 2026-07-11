using System;
using System.Threading;
using System.Threading.Tasks;
using LPS.Domain.Common;
using LPS.Domain.Common.Interfaces;
using LPS.Infrastructure.VariableServices.GlobalVariableManager;

namespace LPS.Infrastructure.PlaceHolderService.Methods
{
    /// <summary>
    /// $equal(a=..., b=..., tolerance=0, variable=name) — returns "true" when a equals b, else "false".
    /// An optional 'tolerance' allows a maximum absolute difference (|a - b| &lt;= tolerance).
    /// </summary>
    public sealed class EqualMethod : MethodBase
    {
        public EqualMethod(ParameterExtractorService p, ILogger l, IRuntimeOperationIdProvider op, IVariableManager v, Lazy<IPlaceholderResolverService> r)
            : base(p, l, op, v, r)
        {
        }

        public override string Name => "equal";

        public override async Task<string> ExecuteAsync(string parameters, string sessionId, CancellationToken token)
        {
            string variableName = string.Empty;

            try
            {
                variableName = await _params.ExtractStringAsync(parameters, "variable", string.Empty, sessionId, token);

                var a = await ResolveNumberParamAsync(parameters, "a", sessionId, token);
                var b = await ResolveNumberParamAsync(parameters, "b", sessionId, token);
                if (a is null || b is null)
                {
                    await _logger.LogAsync(_op.OperationId, "equal failed. Both numeric operands 'a' and 'b' are required.", LPSLoggingLevel.Warning, token);
                    await StoreVariableIfNeededAsync(variableName, "false", token);
                    return "false";
                }

                var tolerance = await ResolveNumberParamAsync(parameters, "tolerance", sessionId, token) ?? 0d;
                var result = Math.Abs(a.Value - b.Value) <= Math.Abs(tolerance);
                var text = result ? "true" : "false";

                await StoreVariableIfNeededAsync(variableName, text, token);
                return text;
            }
            catch (Exception ex)
            {
                await _logger.LogAsync(_op.OperationId, $"equal failed. {ex}", LPSLoggingLevel.Error, token);
                await StoreVariableIfNeededAsync(variableName, "false", token);
                return "false";
            }
        }
    }
}
