using System;
using System.Threading;
using System.Threading.Tasks;
using LPS.Domain.Common;
using LPS.Domain.Common.Interfaces;
using LPS.Infrastructure.LPSClients.SessionManager;
using LPS.Infrastructure.VariableServices.GlobalVariableManager;

namespace LPS.Infrastructure.PlaceHolderService.Methods
{
    /// <summary>$smallerthanorequal(a=..., b=..., variable=name) — returns "true" when a &lt;= b, else "false".</summary>
    public sealed class SmallerThanOrEqualMethod : MethodBase
    {
        public SmallerThanOrEqualMethod(ParameterExtractorService p, ILogger l, IRuntimeOperationIdProvider op, IVariableManager v, Lazy<IPlaceholderResolverService> r, ISessionManager sessionManager)
            : base(p, l, op, v, r, sessionManager)
        {
        }

        public override string Name => "smallerthanorequal";

        public override async Task<string> ExecuteAsync(string parameters, string sessionId, CancellationToken token)
        {
            string variableName = string.Empty;
            bool isGlobal = false;

            try
            {
                variableName = await _params.ExtractStringAsync(parameters, "variable", string.Empty, sessionId, token);
                var numberVariable = await _params.ExtractStringAsync(parameters, "numberVariable", string.Empty, sessionId, token);
                isGlobal = await _params.ExtractBoolAsync(parameters, "isGlobal", false, sessionId, token);

                var a = await ResolveNumberParamAsync(parameters, "a", sessionId, token);
                var b = await ResolveNumberParamAsync(parameters, "b", sessionId, token);
                if (a is null || b is null)
                {
                    await _logger.LogAsync(_op.OperationId, "smallerthanorequal failed. Both numeric operands 'a' and 'b' are required.", LPSLoggingLevel.Warning, token);
                    await StoreStringVariableAsync(variableName, "false", token, sessionId, isGlobal);
                    return "false";
                }

                var text = (a.Value <= b.Value) ? "true" : "false";
                await StoreStringVariableAsync(variableName, text, token, sessionId, isGlobal);
                if (!string.IsNullOrWhiteSpace(numberVariable))
                    await StoreNumberResultAsync(numberVariable, Math.Min(a.Value, b.Value), string.Empty, token, sessionId: sessionId, isGlobal: isGlobal);
                return text;
            }
            catch (Exception ex)
            {
                await _logger.LogAsync(_op.OperationId, $"smallerthanorequal failed. {ex}", LPSLoggingLevel.Error, token);
                await StoreStringVariableAsync(variableName, "false", token, sessionId, isGlobal);
                return "false";
            }
        }
    }
}
