using System;
using System.Threading;
using System.Threading.Tasks;
using LPS.Domain.Common;
using LPS.Domain.Common.Interfaces;
using LPS.Infrastructure.VariableServices.GlobalVariableManager;

namespace LPS.Infrastructure.PlaceHolderService.Methods
{
    /// <summary>$divide(a=..., b=..., variable=name, as=...) — a / b (always a double). Guards divide-by-zero.</summary>
    public sealed class DivideMethod : MethodBase
    {
        public DivideMethod(ParameterExtractorService p, ILogger l, IRuntimeOperationIdProvider op, IVariableManager v, Lazy<IPlaceholderResolverService> r)
            : base(p, l, op, v, r)
        {
        }

        public override string Name => "divide";

        public override async Task<string> ExecuteAsync(string parameters, string sessionId, CancellationToken token)
        {
            string variableName = string.Empty;

            try
            {
                variableName = await _params.ExtractStringAsync(parameters, "variable", string.Empty, sessionId, token);
                var asType = await _params.ExtractStringAsync(parameters, "as", string.Empty, sessionId, token);

                var a = await ResolveNumberParamAsync(parameters, "a", sessionId, token);
                var b = await ResolveNumberParamAsync(parameters, "b", sessionId, token);
                if (a is null || b is null)
                    return await FailNumberAsync(variableName, "divide", token, "Both numeric operands 'a' and 'b' are required.");

                if (b.Value == 0)
                    return await FailNumberAsync(variableName, "divide", token, "Division by zero is not allowed.");

                return await StoreNumberResultAsync(variableName, a.Value / b.Value, asType, token, forceDouble: true);
            }
            catch (Exception ex)
            {
                await _logger.LogAsync(_op.OperationId, $"divide failed. {ex}", LPSLoggingLevel.Error, token);
                await StoreVariableIfNeededAsync(variableName, string.Empty, token);
                return string.Empty;
            }
        }
    }
}
