using System;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using LPS.Domain.Common;
using LPS.Domain.Common.Interfaces;
using LPS.Infrastructure.VariableServices.GlobalVariableManager;

namespace LPS.Infrastructure.PlaceHolderService.Methods
{
    /// <summary>
    /// $setvariable(variable=name, value=..., as=int|double|decimal|bool|string|json)
    /// Resolves 'value' and stores it (typed) into 'variable'. Alias: $set(...).
    /// </summary>
    public sealed class SetVariableMethod : MethodBase
    {
        public SetVariableMethod(ParameterExtractorService p, ILogger l, IRuntimeOperationIdProvider op, IVariableManager v, Lazy<IPlaceholderResolverService> r)
            : base(p, l, op, v, r)
        {
        }

        public override string Name => "setvariable";

        public override async Task<string> ExecuteAsync(string parameters, string sessionId, CancellationToken token)
        {
            string variableName = string.Empty;

            try
            {
                variableName = await _params.ExtractStringAsync(parameters, "variable", string.Empty, sessionId, token);
                var asType = await _params.ExtractStringAsync(parameters, "as", string.Empty, sessionId, token);
                var value = await _params.ExtractStringAsync(parameters, "value", string.Empty, sessionId, token) ?? string.Empty;

                if (string.IsNullOrWhiteSpace(variableName))
                {
                    await _logger.LogAsync(_op.OperationId, "setvariable failed. A 'variable' name is required.", LPSLoggingLevel.Warning, token);
                    return value;
                }

                JToken jt;
                if (string.Equals(asType, "json", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(asType, "jsonstring", StringComparison.OrdinalIgnoreCase))
                {
                    try { jt = JToken.Parse(value); }
                    catch { jt = new JValue(value); }
                }
                else
                {
                    jt = new JValue(value);
                }

                await StoreTypedVariableAsync(variableName, jt, asType, token);
                return value;
            }
            catch (Exception ex)
            {
                await _logger.LogAsync(_op.OperationId, $"setvariable failed. {ex}", LPSLoggingLevel.Error, token);
                return string.Empty;
            }
        }
    }
}
