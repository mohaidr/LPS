using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LPS.Domain.Common;
using LPS.Domain.Common.Interfaces;
using LPS.Infrastructure.VariableServices.GlobalVariableManager;

namespace LPS.Infrastructure.PlaceHolderService.Methods
{
    /// <summary>$multiply(source=[...], variable=name, as=...) — product of all numeric values.</summary>
    public sealed class MultiplyMethod : MethodBase
    {
        public MultiplyMethod(ParameterExtractorService p, ILogger l, IRuntimeOperationIdProvider op, IVariableManager v, Lazy<IPlaceholderResolverService> r)
            : base(p, l, op, v, r)
        {
        }

        public override string Name => "multiply";

        public override async Task<string> ExecuteAsync(string parameters, string sessionId, CancellationToken token)
        {
            string variableName = string.Empty;

            try
            {
                variableName = await _params.ExtractStringAsync(parameters, "variable", string.Empty, sessionId, token);
                var asType = await _params.ExtractStringAsync(parameters, "as", string.Empty, sessionId, token);

                var values = await ResolveNumberArrayAsync(parameters, sessionId, token);
                if (values.Count == 0)
                    return await FailNumberAsync(variableName, "multiply", token);

                double product = values.Aggregate(1.0, (acc, x) => acc * x);
                return await StoreNumberResultAsync(variableName, product, asType, token);
            }
            catch (Exception ex)
            {
                await _logger.LogAsync(_op.OperationId, $"multiply failed. {ex}", LPSLoggingLevel.Error, token);
                await StoreVariableIfNeededAsync(variableName, string.Empty, token);
                return string.Empty;
            }
        }
    }
}
