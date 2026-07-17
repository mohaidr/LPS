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
    /// Resolves 'value' and stores it (typed) into 'variable'. When 'as' is a numeric type, an arithmetic
    /// 'value' (e.g. 2+3, 10/4) is evaluated before storing. Alias: $set(...).
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

                var jt = BuildValueToken(value, asType);

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
