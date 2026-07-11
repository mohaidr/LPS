using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LPS.Domain.Common;
using LPS.Domain.Common.Interfaces;
using LPS.Infrastructure.VariableServices.GlobalVariableManager;

namespace LPS.Infrastructure.PlaceHolderService.Methods
{
    /// <summary>
    /// $strcat(values=[a, b, c], separator=..., variable=name) — joins the given values into a single
    /// string. Values are placeholder‑resolved first; the optional separator is placed between them.
    /// A bracketed list ([a, b, c]) is split on commas; any other value is used as a single item.
    /// </summary>
    public sealed class StrcatMethod : MethodBase
    {
        public StrcatMethod(ParameterExtractorService p, ILogger l, IRuntimeOperationIdProvider op, IVariableManager v, Lazy<IPlaceholderResolverService> r)
            : base(p, l, op, v, r)
        {
        }

        public override string Name => "strcat";

        public override async Task<string> ExecuteAsync(string parameters, string sessionId, CancellationToken token)
        {
            string variableName = string.Empty;

            try
            {
                var values = await _params.ExtractStringAsync(parameters, "values", string.Empty, sessionId, token);
                var separator = Unquote(await _params.ExtractStringAsync(parameters, "separator", string.Empty, sessionId, token));
                variableName = await _params.ExtractStringAsync(parameters, "variable", string.Empty, sessionId, token);

                var result = Concatenate(values, separator);

                await StoreVariableIfNeededAsync(variableName, result, token);
                return result;
            }
            catch (Exception ex)
            {
                await _logger.LogAsync(_op.OperationId, $"strcat failed. {ex}", LPSLoggingLevel.Error, token);
                await StoreVariableIfNeededAsync(variableName, string.Empty, token);
                return string.Empty;
            }
        }

        private static string Concatenate(string values, string separator)
        {
            if (string.IsNullOrEmpty(values))
                return string.Empty;

            var trimmed = values.Trim();

            // A bracketed list ([a, b, c]) is split on commas; anything else is a single value.
            if (trimmed.Length >= 2 && trimmed[0] == '[' && trimmed[^1] == ']')
            {
                var inner = trimmed.Substring(1, trimmed.Length - 2);
                var parts = inner.Split(',').Select(p => Unquote(p.Trim()));
                return string.Join(separator, parts);
            }

            return Unquote(trimmed);
        }

        // Strips a single layer of surrounding double quotes so a caller can preserve leading/trailing
        // whitespace (e.g. separator=" ") that the parameter extractor would otherwise trim away.
        private static string Unquote(string value)
        {
            if (value != null && value.Length >= 2 && value[0] == '"' && value[^1] == '"')
                return value.Substring(1, value.Length - 2);
            return value ?? string.Empty;
        }
    }
}
