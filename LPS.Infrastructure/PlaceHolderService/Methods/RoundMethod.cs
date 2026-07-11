using System;
using System.Threading;
using System.Threading.Tasks;
using LPS.Domain.Common;
using LPS.Domain.Common.Interfaces;
using LPS.Infrastructure.VariableServices.GlobalVariableManager;

namespace LPS.Infrastructure.PlaceHolderService.Methods
{
    /// <summary>
    /// $round(source=..., digits=0, variable=name, as=...) — rounds to 'digits' decimal places
    /// (away from zero). Source may be positional.
    /// </summary>
    public sealed class RoundMethod : MethodBase
    {
        public RoundMethod(ParameterExtractorService p, ILogger l, IRuntimeOperationIdProvider op, IVariableManager v, Lazy<IPlaceholderResolverService> r)
            : base(p, l, op, v, r)
        {
        }

        public override string Name => "round";

        public override async Task<string> ExecuteAsync(string parameters, string sessionId, CancellationToken token)
        {
            string variableName = string.Empty;

            try
            {
                variableName = await _params.ExtractStringAsync(parameters, "variable", string.Empty, sessionId, token);
                var asType = await _params.ExtractStringAsync(parameters, "as", string.Empty, sessionId, token);
                var digits = await _params.ExtractNumberAsync(parameters, "digits", 0, sessionId, token);

                var value = await ResolveNumberParamAsync(parameters, "source", sessionId, token, allowPositional: true);
                if (value is null)
                    return await FailNumberAsync(variableName, "round", token, "A numeric 'source' is required.");

                if (digits < 0) digits = 0;
                if (digits > 15) digits = 15;

                double rounded = Math.Round(value.Value, digits, MidpointRounding.AwayFromZero);
                return await StoreNumberResultAsync(variableName, rounded, asType, token, forceDouble: digits > 0);
            }
            catch (Exception ex)
            {
                await _logger.LogAsync(_op.OperationId, $"round failed. {ex}", LPSLoggingLevel.Error, token);
                await StoreVariableIfNeededAsync(variableName, string.Empty, token);
                return string.Empty;
            }
        }
    }
}
