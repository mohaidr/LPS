using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using LPS.Domain.Common;
using LPS.Domain.Common.Interfaces;
using LPS.Domain.Domain.Common.Interfaces;
using LPS.Infrastructure.VariableServices.GlobalVariableManager;

namespace LPS.Infrastructure.PlaceHolderService.Methods
{
    /// <summary>
    /// $setvariableif(variable=name, condition=${count} > 5, value=big, default=small, as=string)
    /// Declarative conditional store: evaluates <c>condition</c> (a boolean Flee expression) and stores
    /// <c>value</c> into <c>variable</c> when it is true. When it is false, stores <c>default</c> if one was
    /// supplied; otherwise the variable is left untouched. Both <c>value</c> and <c>default</c> are resolved
    /// only for the branch actually taken, and stored (typed) via the same <c>as</c> modes as $find /
    /// $setvariable (auto | json | int | double | decimal | bool | string). Alias: $setif(...).
    /// </summary>
    public sealed class SetVariableIfMethod : ConditionalMethodBase
    {
        public SetVariableIfMethod(
            ParameterExtractorService p,
            ILogger l,
            IRuntimeOperationIdProvider op,
            IVariableManager v,
            Lazy<IPlaceholderResolverService> r,
            Lazy<IExpressionEvaluator> expressionEvaluator)
            : base(p, l, op, v, r, expressionEvaluator)
        {
        }

        public override string Name => "setvariableif";

        public override async Task<string> ExecuteAsync(string parameters, string sessionId, CancellationToken token)
        {
            string variableName = string.Empty;

            try
            {
                var args = _params.ParseParameters(parameters);

                variableName = await ResolveArgAsync(args, "variable", string.Empty, sessionId, token);
                var asType = (await ResolveArgAsync(args, "as", "auto", sessionId, token)).Trim();

                // 'value' and 'default' stay RAW and are resolved only for the branch that is actually taken.
                var valueRaw = args.GetValueOrDefault("value", string.Empty);
                var hasDefault = args.ContainsKey("default");
                var defaultRaw = args.GetValueOrDefault("default", string.Empty);

                if (string.IsNullOrWhiteSpace(variableName))
                {
                    await _logger.LogAsync(_op.OperationId, "setvariableif failed. A 'variable' name is required.", LPSLoggingLevel.Warning, token);
                    return string.Empty;
                }

                var matched = await TryEvaluateConditionAsync(args, sessionId, token);
                if (matched is null)
                {
                    await _logger.LogAsync(_op.OperationId, "setvariableif failed. A 'condition' is required.", LPSLoggingLevel.Warning, token);
                    return string.Empty;
                }

                if (matched.Value)
                {
                    return await ResolveAndStoreAsync(variableName, valueRaw, asType, sessionId, token);
                }

                if (hasDefault)
                {
                    return await ResolveAndStoreAsync(variableName, defaultRaw, asType, sessionId, token);
                }

                // Condition false and no 'default' supplied: leave the variable untouched.
                return string.Empty;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                await _logger.LogAsync(_op.OperationId, $"setvariableif failed. {ex}", LPSLoggingLevel.Error, token);
                return string.Empty;
            }
        }
    }
}
