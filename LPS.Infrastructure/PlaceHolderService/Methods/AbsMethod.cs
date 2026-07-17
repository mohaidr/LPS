using System;
using System.Threading;
using System.Threading.Tasks;
using LPS.Domain.Common;
using LPS.Domain.Common.Interfaces;
using LPS.Infrastructure.VariableServices.GlobalVariableManager;

namespace LPS.Infrastructure.PlaceHolderService.Methods
{
    /// <summary>$abs(source=..., variable=name, as=...) — absolute value. Source may be positional.</summary>
    public sealed class AbsMethod : MethodBase
    {
        public AbsMethod(ParameterExtractorService p, ILogger l, IRuntimeOperationIdProvider op, IVariableManager v, Lazy<IPlaceholderResolverService> r)
            : base(p, l, op, v, r)
        {
        }

        public override string Name => "abs";

        public override async Task<string> ExecuteAsync(string parameters, string sessionId, CancellationToken token)
        {
            string variableName = string.Empty;

            try
            {
                variableName = await _params.ExtractStringAsync(parameters, "variable", string.Empty, sessionId, token);
                var asType = await _params.ExtractStringAsync(parameters, "as", string.Empty, sessionId, token);

                var value = await ResolveNumberParamAsync(parameters, "source", sessionId, token, allowPositional: true);
                if (value is null)
                    return await FailNumberAsync(variableName, "abs", token, "A numeric 'source' is required.");

                return await StoreNumberResultAsync(variableName, Math.Abs(value.Value), asType, token);
            }
            catch (Exception ex)
            {
                await _logger.LogAsync(_op.OperationId, $"abs failed. {ex}", LPSLoggingLevel.Error, token);
                await StoreStringVariableAsync(variableName, string.Empty, token);
                return string.Empty;
            }
        }
    }
}
