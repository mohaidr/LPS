using System;
using System.Threading;
using System.Threading.Tasks;
using LPS.Domain.Common;
using LPS.Domain.Common.Interfaces;
using LPS.Infrastructure.LPSClients.SessionManager;
using LPS.Infrastructure.VariableServices.GlobalVariableManager;

namespace LPS.Infrastructure.PlaceHolderService.Methods
{
    public sealed class ToUpperCaseMethod : MethodBase
    {
        public ToUpperCaseMethod(ParameterExtractorService p, ILogger l, IRuntimeOperationIdProvider op, IVariableManager v, Lazy<IPlaceholderResolverService> r, ISessionManager sessionManager)
            : base(p, l, op, v, r, sessionManager)
        {
        }

        public override string Name => "touppercase";

        public override async Task<string> ExecuteAsync(string parameters, string sessionId, CancellationToken token)
        {
            string variableName = string.Empty;
            bool isGlobal = false;

            try
            {
                var source = await _params.ExtractStringAsync(parameters, "source", string.Empty, sessionId, token);
                if (string.IsNullOrWhiteSpace(source))
                {
                    source = ExtractPositionalParameter(parameters);
                }

                variableName = await _params.ExtractStringAsync(parameters, "variable", string.Empty, sessionId, token);
                isGlobal = await _params.ExtractBoolAsync(parameters, "isGlobal", false, sessionId, token);

                if (string.IsNullOrWhiteSpace(source))
                {
                    await _logger.LogAsync(_op.OperationId, "touppercase failed. No source expression was provided.", LPSLoggingLevel.Warning, token);
                    await StoreTypedVariableAsync(variableName, BuildValueToken(string.Empty, "string"), "string", token, isGlobal, sessionId);
                    return string.Empty;
                }

                var resolved = await _resolver.Value.ResolvePlaceholdersAsync<string>(source, sessionId, token);
                var result = (resolved ?? string.Empty).ToUpperInvariant();

                await StoreTypedVariableAsync(variableName, BuildValueToken(result, "string"), "string", token, isGlobal, sessionId);
                return result;
            }
            catch (Exception ex)
            {
                await _logger.LogAsync(_op.OperationId, $"touppercase failed. {ex}", LPSLoggingLevel.Error, token);
                await StoreTypedVariableAsync(variableName, BuildValueToken(string.Empty, "string"), "string", token, isGlobal, sessionId);
                return string.Empty;
            }
        }

        private static string ExtractPositionalParameter(string parameters)
        {
            if (string.IsNullOrWhiteSpace(parameters))
            {
                return string.Empty;
            }

            foreach (var part in parameters.Split(','))
            {
                var trimmed = part.Trim();
                if (!string.IsNullOrWhiteSpace(trimmed) && !trimmed.Contains('='))
                {
                    return trimmed;
                }
            }

            return string.Empty;
        }
    }
}