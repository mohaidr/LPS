using System;
using System.Threading;
using System.Threading.Tasks;
using LPS.Domain.Common;
using LPS.Domain.Common.Interfaces;
using LPS.Infrastructure.VariableServices.GlobalVariableManager;

namespace LPS.Infrastructure.PlaceHolderService.Methods
{
    public sealed class ToLowerCaseMethod : MethodBase
    {
        public ToLowerCaseMethod(ParameterExtractorService p, ILogger l, IRuntimeOperationIdProvider op, IVariableManager v, Lazy<IPlaceholderResolverService> r)
            : base(p, l, op, v, r)
        {
        }

        public override string Name => "tolowercase";

        public override async Task<string> ExecuteAsync(string parameters, string sessionId, CancellationToken token)
        {
            string variableName = string.Empty;

            try
            {
                var source = await _params.ExtractStringAsync(parameters, "source", string.Empty, sessionId, token);
                if (string.IsNullOrWhiteSpace(source))
                {
                    source = ExtractPositionalParameter(parameters);
                }

                variableName = await _params.ExtractStringAsync(parameters, "variable", string.Empty, sessionId, token);

                if (string.IsNullOrWhiteSpace(source))
                {
                    await _logger.LogAsync(_op.OperationId, "tolowercase failed. No source expression was provided.", LPSLoggingLevel.Warning, token);
                    await StoreVariableIfNeededAsync(variableName, string.Empty, token);
                    return string.Empty;
                }

                var resolved = await _resolver.Value.ResolvePlaceholdersAsync<string>(source, sessionId, token);
                var result = (resolved ?? string.Empty).ToLowerInvariant();

                await StoreVariableIfNeededAsync(variableName, result, token);
                return result;
            }
            catch (Exception ex)
            {
                await _logger.LogAsync(_op.OperationId, $"tolowercase failed. {ex}", LPSLoggingLevel.Error, token);
                await StoreVariableIfNeededAsync(variableName, string.Empty, token);
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