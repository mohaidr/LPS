
using System;
using System.Threading;
using System.Threading.Tasks;
using LPS.Domain.Common;
using LPS.Domain.Common.Interfaces;
using LPS.Infrastructure.LPSClients.SessionManager;
using LPS.Infrastructure.VariableServices.GlobalVariableManager;

namespace LPS.Infrastructure.PlaceHolderService.Methods
{
    public sealed class TimestampMethod : MethodBase
    {
        public TimestampMethod(ParameterExtractorService p, ILogger l, IRuntimeOperationIdProvider op, IVariableManager v, Lazy<IPlaceholderResolverService> r, ISessionManager sessionManager)
            : base(p,l,op,v,r, sessionManager) { }

        public override string Name => "timestamp";

        public override async Task<string> ExecuteAsync(string parameters, string sessionId, CancellationToken token)
        {
            string variableName = string.Empty;
            bool isGlobal = false;
            try
            {
                string format = await _params.ExtractStringAsync(parameters, "format", "yyyy-MM-ddTHH:mm:ss", sessionId, token);
                int offsetHours = await _params.ExtractNumberAsync(parameters, "offsetHours", 0, sessionId, token);
                variableName = await _params.ExtractStringAsync(parameters, "variable", "", sessionId, token);
                isGlobal = await _params.ExtractBoolAsync(parameters, "isGlobal", false, sessionId, token);

                DateTime dt = DateTime.UtcNow.AddHours(offsetHours);
                string result = dt.ToString(format);
                await StoreTypedVariableAsync(variableName, BuildValueToken(result, "string"), "string", token, isGlobal, sessionId);
                return result;
            }
            catch (Exception ex)
            {
                await _logger.LogAsync(_op.OperationId, $"timestamp failed. {ex}", LPSLoggingLevel.Error, token);
                await StoreTypedVariableAsync(variableName, BuildValueToken(string.Empty, "string"), "string", token, isGlobal, sessionId);
                return string.Empty;
            }
        }
    }
}
