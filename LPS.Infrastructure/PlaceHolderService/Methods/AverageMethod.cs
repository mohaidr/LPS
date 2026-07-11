using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LPS.Domain.Common;
using LPS.Domain.Common.Interfaces;
using LPS.Infrastructure.VariableServices.GlobalVariableManager;

namespace LPS.Infrastructure.PlaceHolderService.Methods
{
    /// <summary>$average(source=[...], variable=name, as=...) — arithmetic mean. Alias: $avg(...).</summary>
    public sealed class AverageMethod : MethodBase
    {
        public AverageMethod(ParameterExtractorService p, ILogger l, IRuntimeOperationIdProvider op, IVariableManager v, Lazy<IPlaceholderResolverService> r)
            : base(p, l, op, v, r)
        {
        }

        public override string Name => "average";

        public override async Task<string> ExecuteAsync(string parameters, string sessionId, CancellationToken token)
        {
            string variableName = string.Empty;

            try
            {
                variableName = await _params.ExtractStringAsync(parameters, "variable", string.Empty, sessionId, token);
                var asType = await _params.ExtractStringAsync(parameters, "as", string.Empty, sessionId, token);

                var values = await ResolveNumberArrayAsync(parameters, sessionId, token);
                if (values.Count == 0)
                    return await FailNumberAsync(variableName, "average", token);

                return await StoreNumberResultAsync(variableName, values.Average(), asType, token, forceDouble: true);
            }
            catch (Exception ex)
            {
                await _logger.LogAsync(_op.OperationId, $"average failed. {ex}", LPSLoggingLevel.Error, token);
                await StoreVariableIfNeededAsync(variableName, string.Empty, token);
                return string.Empty;
            }
        }
    }
}
