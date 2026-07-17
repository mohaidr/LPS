
using System;
using System.Threading;
using System.Threading.Tasks;
using LPS.Domain.Common;
using LPS.Domain.Common.Interfaces;
using LPS.Infrastructure.LPSClients.SessionManager;
using LPS.Infrastructure.VariableServices.GlobalVariableManager;

namespace LPS.Infrastructure.PlaceHolderService.Methods
{
    public sealed class ReadMethod : MethodBase
    {
        public ReadMethod(ISessionManager sessionManager,
                          ParameterExtractorService p, ILogger l, IRuntimeOperationIdProvider op, IVariableManager v, Lazy<IPlaceholderResolverService> r)
            : base(p,l,op,v,r, sessionManager)
        {
        }

        public override string Name => "read";

        public override async Task<string> ExecuteAsync(string parameters, string sessionId, CancellationToken token)
        {
            string variableName = string.Empty;
            bool isGlobal = false;
            try
            {
                variableName = await _params.ExtractStringAsync(parameters, "variable", "", sessionId, token);
                isGlobal = await _params.ExtractBoolAsync(parameters, "isGlobal", false, sessionId, token);

                var result = await SourceReader.ReadAsync(_params, _session, _variables, parameters, sessionId, token);

                await StoreStringVariableAsync(variableName, result, token, sessionId, isGlobal);
                return result;
            }
            catch (Exception ex)
            {
                await _logger.LogAsync(_op.OperationId, $"read failed. {ex}", LPSLoggingLevel.Error, token);
                await StoreStringVariableAsync(variableName, string.Empty, token, sessionId, isGlobal);
                return string.Empty;
            }
        }
    }
}
