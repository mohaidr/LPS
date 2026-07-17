
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LPS.Domain.Common;
using LPS.Domain.Common.Interfaces;
using LPS.Infrastructure.LPSClients.SessionManager;
using LPS.Infrastructure.VariableServices.GlobalVariableManager;

namespace LPS.Infrastructure.PlaceHolderService.Methods
{
    public sealed class FormatMethod : MethodBase
    {
        public FormatMethod(ParameterExtractorService p, ILogger l, IRuntimeOperationIdProvider op, IVariableManager v, Lazy<IPlaceholderResolverService> r, ISessionManager sessionManager)
            : base(p,l,op,v,r, sessionManager) { }

        public override string Name => "format";

        public override async Task<string> ExecuteAsync(string parameters, string sessionId, CancellationToken token)
        {
            string variableName = string.Empty;
            string template = string.Empty;
            string args = string.Empty;
            bool isGlobal = false;
            try
            {
                template = await _params.ExtractStringAsync(parameters, "template", string.Empty, sessionId, token);
                args = await _params.ExtractStringAsync(parameters, "args", string.Empty, sessionId, token);
                variableName = await _params.ExtractStringAsync(parameters, "variable", "", sessionId, token);
                isGlobal = await _params.ExtractBoolAsync(parameters, "isGlobal", false, sessionId, token);

                string result = string.Format(template, args.Split(",").ToArray());
                result = await _resolver.Value.ResolvePlaceholdersAsync<string>(result, sessionId, token);
                await StoreStringVariableAsync(variableName, result, token, sessionId, isGlobal);
                return result;
            }
            catch (Exception ex)
            {
                await _logger.LogAsync(_op.OperationId, $"format failed (template='{template}', args='{args}'). {ex}", LPSLoggingLevel.Error, token);
                await StoreStringVariableAsync(variableName, string.Empty, token, sessionId, isGlobal);
                return string.Empty;
            }
        }
    }
}
