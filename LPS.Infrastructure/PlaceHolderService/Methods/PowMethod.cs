using System;
using System.Threading;
using System.Threading.Tasks;
using LPS.Domain.Common;
using LPS.Domain.Common.Interfaces;
using LPS.Infrastructure.LPSClients.SessionManager;
using LPS.Infrastructure.VariableServices.GlobalVariableManager;

namespace LPS.Infrastructure.PlaceHolderService.Methods
{
    /// <summary>$pow(base=..., exponent=..., variable=name, as=...) — base raised to exponent.</summary>
    public sealed class PowMethod : MethodBase
    {
        public PowMethod(ParameterExtractorService p, ILogger l, IRuntimeOperationIdProvider op, IVariableManager v, Lazy<IPlaceholderResolverService> r, ISessionManager sessionManager)
            : base(p, l, op, v, r, sessionManager)
        {
        }

        public override string Name => "pow";

        public override async Task<string> ExecuteAsync(string parameters, string sessionId, CancellationToken token)
        {
            string variableName = string.Empty;
            bool isGlobal = false;

            try
            {
                variableName = await _params.ExtractStringAsync(parameters, "variable", string.Empty, sessionId, token);
                var asType = await _params.ExtractStringAsync(parameters, "as", string.Empty, sessionId, token);
                isGlobal = await _params.ExtractBoolAsync(parameters, "isGlobal", false, sessionId, token);

                var baseValue = await ResolveNumberParamAsync(parameters, "base", sessionId, token);
                var exponent = await ResolveNumberParamAsync(parameters, "exponent", sessionId, token);
                if (baseValue is null || exponent is null)
                    return await FailNumberAsync(variableName, "pow", token, "Both 'base' and 'exponent' are required.", sessionId: sessionId, isGlobal: isGlobal);

                return await StoreNumberResultAsync(variableName, Math.Pow(baseValue.Value, exponent.Value), asType, token, sessionId: sessionId, isGlobal: isGlobal);
            }
            catch (Exception ex)
            {
                await _logger.LogAsync(_op.OperationId, $"pow failed. {ex}", LPSLoggingLevel.Error, token);
                await StoreTypedVariableAsync(variableName, BuildValueToken(string.Empty, "string"), "string", token, isGlobal, sessionId);
                return string.Empty;
            }
        }
    }
}
