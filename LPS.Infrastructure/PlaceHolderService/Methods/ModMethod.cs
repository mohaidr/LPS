using System;
using System.Threading;
using System.Threading.Tasks;
using LPS.Domain.Common;
using LPS.Domain.Common.Interfaces;
using LPS.Infrastructure.VariableServices.GlobalVariableManager;

namespace LPS.Infrastructure.PlaceHolderService.Methods
{
    /// <summary>$mod(a=..., b=..., variable=name, as=...) — remainder of a / b. Guards divide-by-zero.</summary>
    public sealed class ModMethod : MethodBase
    {
        public ModMethod(ParameterExtractorService p, ILogger l, IRuntimeOperationIdProvider op, IVariableManager v, Lazy<IPlaceholderResolverService> r)
            : base(p, l, op, v, r)
        {
        }

        public override string Name => "mod";

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
                    return await FailNumberAsync(variableName, "mod", token, "Both numeric operands 'a' and 'b' are required.");

                if (b.Value == 0)
                    return await FailNumberAsync(variableName, "mod", token, "Modulo by zero is not allowed.");

                return await StoreNumberResultAsync(variableName, a.Value % b.Value, asType, token);
            }
            catch (Exception ex)
            {
                await _logger.LogAsync(_op.OperationId, $"mod failed. {ex}", LPSLoggingLevel.Error, token);
                await StoreVariableIfNeededAsync(variableName, string.Empty, token);
                return string.Empty;
            }
        }
    }
}
