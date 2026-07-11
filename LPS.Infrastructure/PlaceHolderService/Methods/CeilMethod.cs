using System;
using System.Threading;
using System.Threading.Tasks;
using LPS.Domain.Common;
using LPS.Domain.Common.Interfaces;
using LPS.Infrastructure.VariableServices.GlobalVariableManager;

namespace LPS.Infrastructure.PlaceHolderService.Methods
{
    /// <summary>$ceil(source=..., variable=name, as=...) — smallest integer &gt;= source. Source may be positional.</summary>
    public sealed class CeilMethod : MethodBase
    {
        public CeilMethod(ParameterExtractorService p, ILogger l, IRuntimeOperationIdProvider op, IVariableManager v, Lazy<IPlaceholderResolverService> r)
            : base(p, l, op, v, r)
        {
        }

        public override string Name => "ceil";

        public override async Task<string> ExecuteAsync(string parameters, string sessionId, CancellationToken token)
        {
            string variableName = string.Empty;

            try
            {
                variableName = await _params.ExtractStringAsync(parameters, "variable", string.Empty, sessionId, token);
                var asType = await _params.ExtractStringAsync(parameters, "as", string.Empty, sessionId, token);

                var value = await ResolveNumberParamAsync(parameters, "source", sessionId, token, allowPositional: true);
                if (value is null)
                    return await FailNumberAsync(variableName, "ceil", token, "A numeric 'source' is required.");

                return await StoreNumberResultAsync(variableName, Math.Ceiling(value.Value), asType, token);
            }
            catch (Exception ex)
            {
                await _logger.LogAsync(_op.OperationId, $"ceil failed. {ex}", LPSLoggingLevel.Error, token);
                await StoreVariableIfNeededAsync(variableName, string.Empty, token);
                return string.Empty;
            }
        }
    }
}
