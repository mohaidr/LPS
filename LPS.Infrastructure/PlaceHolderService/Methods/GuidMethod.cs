
using System;
using System.Threading;
using System.Threading.Tasks;
using LPS.Domain.Common;
using LPS.Domain.Common.Interfaces;
using LPS.Infrastructure.LPSClients.SessionManager;
using LPS.Infrastructure.VariableServices.GlobalVariableManager;

namespace LPS.Infrastructure.PlaceHolderService.Methods
{
    public sealed class GuidMethod : MethodBase
    {
        public GuidMethod(ParameterExtractorService p, ILogger l, IRuntimeOperationIdProvider op, IVariableManager v, Lazy<IPlaceholderResolverService> r, ISessionManager sessionManager)
            : base(p,l,op,v,r, sessionManager) { }

        public override string Name => "guid";

        public override async Task<string> ExecuteAsync(string parameters, string sessionId, CancellationToken token)
        {
            string variableName = await _params.ExtractStringAsync(parameters, "variable", "", sessionId, token);
            bool isGlobal = await _params.ExtractBoolAsync(parameters, "isGlobal", false, sessionId, token);
            string result = Guid.NewGuid().ToString();
            await StoreStringVariableAsync(variableName, result, token, sessionId, isGlobal);
            return result;
        }
    }
}
