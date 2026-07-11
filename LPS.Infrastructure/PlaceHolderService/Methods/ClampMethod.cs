using System;
using System.Threading;
using System.Threading.Tasks;
using LPS.Domain.Common;
using LPS.Domain.Common.Interfaces;
using LPS.Infrastructure.VariableServices.GlobalVariableManager;

namespace LPS.Infrastructure.PlaceHolderService.Methods
{
    /// <summary>$clamp(value=..., min=..., max=..., variable=name, as=...) — bounds value into [min, max].</summary>
    public sealed class ClampMethod : MethodBase
    {
        public ClampMethod(ParameterExtractorService p, ILogger l, IRuntimeOperationIdProvider op, IVariableManager v, Lazy<IPlaceholderResolverService> r)
            : base(p, l, op, v, r)
        {
        }

        public override string Name => "clamp";

        public override async Task<string> ExecuteAsync(string parameters, string sessionId, CancellationToken token)
        {
            string variableName = string.Empty;

            try
            {
                variableName = await _params.ExtractStringAsync(parameters, "variable", string.Empty, sessionId, token);
                var asType = await _params.ExtractStringAsync(parameters, "as", string.Empty, sessionId, token);

                var value = await ResolveNumberParamAsync(parameters, "value", sessionId, token);
                var min = await ResolveNumberParamAsync(parameters, "min", sessionId, token);
                var max = await ResolveNumberParamAsync(parameters, "max", sessionId, token);
                if (value is null || min is null || max is null)
                    return await FailNumberAsync(variableName, "clamp", token, "'value', 'min', and 'max' are all required.");

                double low = min.Value, high = max.Value;
                if (low > high)
                {
                    await _logger.LogAsync(_op.OperationId, "clamp: 'min' was greater than 'max'; swapping the bounds.", LPSLoggingLevel.Warning, token);
                    (low, high) = (high, low);
                }

                double clamped = Math.Min(Math.Max(value.Value, low), high);
                return await StoreNumberResultAsync(variableName, clamped, asType, token);
            }
            catch (Exception ex)
            {
                await _logger.LogAsync(_op.OperationId, $"clamp failed. {ex}", LPSLoggingLevel.Error, token);
                await StoreVariableIfNeededAsync(variableName, string.Empty, token);
                return string.Empty;
            }
        }
    }
}
