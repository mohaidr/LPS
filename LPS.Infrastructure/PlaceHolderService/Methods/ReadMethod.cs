
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
                var asType = await _params.ExtractStringAsync(parameters, "as", "string", sessionId, token);

                var result = await SourceReader.ReadAsync(_params, _session, _variables, parameters, sessionId, token);

                // Store the read content typed by `as` (json/int/double/decimal/bool/string; default `string`
                // preserves the historical behavior). The inline return stays the raw text; `as` only affects
                // the stored variable's type — the same mechanism $readif uses.
                await StoreTypedVariableAsync(variableName, BuildValueToken(result, asType), asType, token, isGlobal, sessionId);
                return result;
            }
            catch (Exception ex)
            {
                await _logger.LogAsync(_op.OperationId, $"read failed. {ex}", LPSLoggingLevel.Error, token);
                await StoreTypedVariableAsync(variableName, BuildValueToken(string.Empty, "string"), "string", token, isGlobal, sessionId);
                return string.Empty;
            }
        }
    }
}
