
using System;
using System.Threading;
using System.Threading.Tasks;
using LPS.Domain.Common;
using LPS.Domain.Common.Interfaces;
using LPS.Infrastructure.LPSClients.SessionManager;
using LPS.Infrastructure.VariableServices.GlobalVariableManager;

namespace LPS.Infrastructure.PlaceHolderService.Methods
{
    public sealed class UuidMethod : MethodBase
    {
        public UuidMethod(ParameterExtractorService p, ILogger l, IRuntimeOperationIdProvider op, IVariableManager v, Lazy<IPlaceholderResolverService> r, ISessionManager sessionManager)
            : base(p,l,op,v,r, sessionManager) { }

        public override string Name => "uuid";

        public override async Task<string> ExecuteAsync(string parameters, string sessionId, CancellationToken token)
        {
            string prefix = await _params.ExtractStringAsync(parameters, "prefix", "", sessionId, token);
            string variableName = await _params.ExtractStringAsync(parameters, "variable", "", sessionId, token);
            bool isGlobal = await _params.ExtractBoolAsync(parameters, "isGlobal", false, sessionId, token);
            string result = prefix + Guid.NewGuid().ToString();
            await StoreStringVariableAsync(variableName, result, token, sessionId, isGlobal);
            return result;
        }
    }
}
