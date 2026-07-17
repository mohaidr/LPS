
using System;
using System.Threading;
using System.Threading.Tasks;
using LPS.Domain.Common;
using LPS.Domain.Common.Interfaces;
using LPS.Infrastructure.LPSClients.SessionManager;
using LPS.Infrastructure.VariableServices.GlobalVariableManager;

namespace LPS.Infrastructure.PlaceHolderService.Methods
{
    public sealed class UrlDecodeMethod : MethodBase
    {
        public UrlDecodeMethod(ParameterExtractorService p, ILogger l, IRuntimeOperationIdProvider op, IVariableManager v, Lazy<IPlaceholderResolverService> r, ISessionManager sessionManager)
            : base(p,l,op,v,r, sessionManager) { }

        public override string Name => "urldecode";

        public override async Task<string> ExecuteAsync(string parameters, string sessionId, CancellationToken token)
        {
            string variableName = string.Empty, value = string.Empty;
            bool isGlobal = false;
            try
            {
                value = await _params.ExtractStringAsync(parameters, "value", string.Empty, sessionId, token);
                variableName = await _params.ExtractStringAsync(parameters, "variable", "", sessionId, token);
                isGlobal = await _params.ExtractBoolAsync(parameters, "isGlobal", false, sessionId, token);
                string result = string.IsNullOrEmpty(value) ? string.Empty : Uri.UnescapeDataString(value);
                await StoreStringVariableAsync(variableName, result, token, sessionId, isGlobal);
                return result;
            }
            catch (Exception ex)
            {
                await _logger.LogAsync(_op.OperationId, $"urldecode failed for '{Truncate(value)}'. {ex}", LPSLoggingLevel.Error, token);
                await StoreStringVariableAsync(variableName, string.Empty, token, sessionId, isGlobal);
                return string.Empty;
            }
        }
    }
}
