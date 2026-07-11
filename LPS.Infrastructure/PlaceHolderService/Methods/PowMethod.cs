using System;
using System.Threading;
using System.Threading.Tasks;
using LPS.Domain.Common;
using LPS.Domain.Common.Interfaces;
using LPS.Infrastructure.VariableServices.GlobalVariableManager;

namespace LPS.Infrastructure.PlaceHolderService.Methods
{
    /// <summary>$pow(base=..., exponent=..., variable=name, as=...) — base raised to exponent.</summary>
    public sealed class PowMethod : MethodBase
    {
        public PowMethod(ParameterExtractorService p, ILogger l, IRuntimeOperationIdProvider op, IVariableManager v, Lazy<IPlaceholderResolverService> r)
            : base(p, l, op, v, r)
        {
        }

        public override string Name => "pow";

        public override async Task<string> ExecuteAsync(string parameters, string sessionId, CancellationToken token)
        {
            string variableName = string.Empty;

            try
            {
                variableName = await _params.ExtractStringAsync(parameters, "variable", string.Empty, sessionId, token);
                var asType = await _params.ExtractStringAsync(parameters, "as", string.Empty, sessionId, token);

                var baseValue = await ResolveNumberParamAsync(parameters, "base", sessionId, token);
                var exponent = await ResolveNumberParamAsync(parameters, "exponent", sessionId, token);
                if (baseValue is null || exponent is null)
                    return await FailNumberAsync(variableName, "pow", token, "Both 'base' and 'exponent' are required.");

                return await StoreNumberResultAsync(variableName, Math.Pow(baseValue.Value, exponent.Value), asType, token);
            }
            catch (Exception ex)
            {
                await _logger.LogAsync(_op.OperationId, $"pow failed. {ex}", LPSLoggingLevel.Error, token);
                await StoreVariableIfNeededAsync(variableName, string.Empty, token);
                return string.Empty;
            }
        }
    }
}
