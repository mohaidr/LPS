using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LPS.Domain.Common;
using LPS.Domain.Common.Interfaces;
using LPS.Infrastructure.VariableServices.GlobalVariableManager;

namespace LPS.Infrastructure.PlaceHolderService.Methods
{
    /// <summary>$min(source=[...], variable=name, as=...) — smallest numeric value.</summary>
    public sealed class MinMethod : MethodBase
    {
        public MinMethod(ParameterExtractorService p, ILogger l, IRuntimeOperationIdProvider op, IVariableManager v, Lazy<IPlaceholderResolverService> r)
            : base(p, l, op, v, r)
        {
        }

        public override string Name => "min";

        public override async Task<string> ExecuteAsync(string parameters, string sessionId, CancellationToken token)
        {
            string variableName = string.Empty;

            try
            {
                variableName = await _params.ExtractStringAsync(parameters, "variable", string.Empty, sessionId, token);
                var asType = await _params.ExtractStringAsync(parameters, "as", string.Empty, sessionId, token);

                var values = await ResolveNumberArrayAsync(parameters, sessionId, token);
                if (values.Count == 0)
                    return await FailNumberAsync(variableName, "min", token);

                return await StoreNumberResultAsync(variableName, values.Min(), asType, token);
            }
            catch (Exception ex)
            {
                await _logger.LogAsync(_op.OperationId, $"min failed. {ex}", LPSLoggingLevel.Error, token);
                await StoreStringVariableAsync(variableName, string.Empty, token);
                return string.Empty;
            }
        }
    }
}
