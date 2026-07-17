using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using LPS.Domain.Common;
using LPS.Domain.Common.Interfaces;
using LPS.Domain.Domain.Common.Interfaces;
using LPS.Infrastructure.LPSClients.SessionManager;
using LPS.Infrastructure.VariableServices.GlobalVariableManager;

namespace LPS.Infrastructure.PlaceHolderService.Methods
{
    /// <summary>
    /// $readif(condition=${enabled} == true, source=file, path=config.txt, variable=cfg, default=, as=string, isGlobal=true)
    /// The conditional counterpart of $read. When <c>condition</c> (a boolean Flee expression) is true it
    /// performs the same read as $read (from a file / env / variable via source|path|name|encoding) and
    /// stores the result. When it is false, stores <c>default</c> if one was supplied; otherwise the variable
    /// is left untouched. The stored value is typed via the same <c>as</c> modes as $find / $setvariableif.
    /// 'isGlobal' (default true) stores globally; set isGlobal=false to store scoped to the current session.
    /// </summary>
    public sealed class ReadIfMethod : ConditionalMethodBase
    {
        public ReadIfMethod(
            ISessionManager sessionManager,
            ParameterExtractorService p,
            ILogger l,
            IRuntimeOperationIdProvider op,
            IVariableManager v,
            Lazy<IPlaceholderResolverService> r,
            Lazy<IExpressionEvaluator> expressionEvaluator)
            : base(p, l, op, v, r, expressionEvaluator, sessionManager)
        {
        }

        public override string Name => "readif";

        public override async Task<string> ExecuteAsync(string parameters, string sessionId, CancellationToken token)
        {
            string variableName = string.Empty;

            try
            {
                var args = _params.ParseParameters(parameters);

                variableName = await ResolveArgAsync(args, "variable", string.Empty, sessionId, token);
                var asType = (await ResolveArgAsync(args, "as", "auto", sessionId, token)).Trim();
                var isGlobal = await _params.ExtractBoolAsync(parameters, "isGlobal", false, sessionId, token);

                // 'default' stays RAW and is resolved only when the condition is false.
                var hasDefault = args.ContainsKey("default");
                var defaultRaw = args.GetValueOrDefault("default", string.Empty);

                if (string.IsNullOrWhiteSpace(variableName))
                {
                    await _logger.LogAsync(_op.OperationId, "readif failed. A 'variable' name is required.", LPSLoggingLevel.Warning, token);
                    return string.Empty;
                }

                var matched = await TryEvaluateConditionAsync(args, sessionId, token);
                if (matched is null)
                {
                    await _logger.LogAsync(_op.OperationId, "readif failed. A 'condition' is required.", LPSLoggingLevel.Warning, token);
                    return string.Empty;
                }

                if (matched.Value)
                {
                    var result = await SourceReader.ReadAsync(_params, _session, _variables, parameters, sessionId, token);
                    return await StoreResolvedAsync(variableName, result, asType, sessionId, isGlobal, token);
                }

                if (hasDefault)
                {
                    return await ResolveAndStoreAsync(variableName, defaultRaw, asType, sessionId, isGlobal, token);
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
                await _logger.LogAsync(_op.OperationId, $"readif failed. {ex}", LPSLoggingLevel.Error, token);
                return string.Empty;
            }
        }
    }
}
