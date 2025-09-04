
using System;
using System.Threading;
using System.Threading.Tasks;
using AsyncKeyedLock;
using LPS.Domain.Common;
using LPS.Domain.Common.Interfaces;
using LPS.Infrastructure.Caching;
using LPS.Infrastructure.Common;
using LPS.Infrastructure.LPSClients.CachService;
using LPS.Infrastructure.VariableServices.GlobalVariableManager;

namespace LPS.Infrastructure.PlaceHolderService.Methods
{
    public sealed class IterateMethod : MethodBase
    {
        public IterateMethod(ParameterExtractorService p, 
                             ILogger l, IRuntimeOperationIdProvider op,
                             IVariableManager v, 
                             Lazy<IPlaceholderResolverService> r,
                             ICacheService<string> memoryCacheService)
            : base(p,l,op,v,r) 
        { 
            _memoryCacheService = memoryCacheService;
        }
        private readonly ICacheService<string> _memoryCacheService;
        private static readonly AsyncKeyedLocker<string> _locker = new();

        public override string Name => "iterate";

        public override async Task<string> ExecuteAsync(string parameters, string sessionId, CancellationToken token)
        {
            string variableName = string.Empty;
            try
            {
                var start = await _params.ExtractNumberAsync(parameters, "start", 0, sessionId, token);
                var end = await _params.ExtractNumberAsync(parameters, "end", 100000, sessionId, token);
                var counterName = await _params.ExtractStringAsync(parameters, "counter", string.Empty, sessionId, token);
                var step = await _params.ExtractNumberAsync(parameters, "step", 1, sessionId, token);
                variableName = await _params.ExtractStringAsync(parameters, "variable", "", sessionId, token);

                if (start >= end)
                {
                    await _logger.LogAsync(_op.OperationId, $"iterate invalid range: start({start}) >= end({end}).", LPSLoggingLevel.Error, token);
                    await StoreVariableIfNeededAsync(variableName, string.Empty, token);
                    return string.Empty;
                }

                var cacheKeySuffix = string.IsNullOrEmpty(counterName) ? string.Empty : $"_{counterName.Trim()}";
                string cacheKey = string.IsNullOrEmpty(sessionId) || !int.TryParse(sessionId, out _)
                    ? $"{CachePrefixes.GlobalCounter}{start}_{end}{cacheKeySuffix}"
                    : $"{CachePrefixes.SessionCounter}{sessionId}_{start}_{end}{cacheKeySuffix}";

                using (await _locker.LockAsync(cacheKey, token))
                {
                    if (!_memoryCacheService.TryGetItem(cacheKey, out string currentValueString) ||
                        !int.TryParse(currentValueString, out int current))
                    {
                        current = start;
                    }
                    else
                    {
                        current += step;
                        if (current > end || current < start)
                        {
                            current = start;
                            await _logger.LogAsync(_op.OperationId, $"iterate reset to start '{start}' for key '{cacheKey}'.", LPSLoggingLevel.Verbose, token);
                        }
                    }

                    string result = current.ToString();
                    await _memoryCacheService.SetItemAsync(cacheKey, result, TimeSpan.MaxValue);
                    await StoreVariableIfNeededAsync(variableName, result, token);
                    return result;
                }
            }
            catch (Exception ex)
            {
                await _logger.LogAsync(_op.OperationId, $"iterate failed. {ex}", LPSLoggingLevel.Error, token);
                await StoreVariableIfNeededAsync(variableName, string.Empty, token);
                return string.Empty;
            }
        }
    }

    public sealed class LoopCounterAliasMethod : IPlaceholderMethod
    {
        private readonly IterateMethod _inner;
        public LoopCounterAliasMethod(IterateMethod inner) { _inner = inner; }
        public string Name => "loopcounter";
        public Task<string> ExecuteAsync(string parameters, string sessionId, CancellationToken token)
            => _inner.ExecuteAsync(parameters, sessionId, token);
    }
}
