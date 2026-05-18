using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LPS.Domain.Common.Interfaces;
using LPS.Infrastructure.Common.Interfaces;
using LPS.Infrastructure.Monitoring.EventListeners;
using LPS.Infrastructure.GRPCClients;
using LPS.Protos.Shared;
using LPS.Infrastructure.GRPCClients.Factory;
using LPS.Infrastructure.Nodes;

namespace LPS.Infrastructure.Watchdog
{
    public enum SuspensionMode
    {
        Any,
        All
    }

    /// <summary>
    /// Watchdog that samples system pressure in the background and gates callers
    /// until their requested host can proceed under a Cool state.
    ///
    /// Design:
    ///  - A single background sampler loop ticks every <see cref="SamplingIntervalMs"/> and owns ALL
    ///    mutable cooling/state bookkeeping. No locks are needed for that state because only the
    ///    sampler mutates it.
    ///  - Callers of <see cref="BalanceAsync"/> register their host and await sampler ticks until
    ///    the evaluated state for that host is <c>Cool</c>.
    ///  - Sampling and state transitions are owned by the sampler loop; callers never resample
    ///    resources directly.
    ///  - <see cref="GC.Collect()"/> is only invoked under real memory pressure and is throttled to
    ///    at most once per <see cref="GcMinIntervalSeconds"/>.
    /// </summary>
    public class Watchdog : IWatchdog, IAsyncDisposable
    {
        private const int SamplingIntervalMs = 1000;
        private const int GcMinIntervalSeconds = 30;

        private readonly ILogger _logger;
        private readonly IRuntimeOperationIdProvider _operationIdProvider;
        private readonly ICustomGrpcClientFactory _customGrpcClientFactory;
        private readonly IClusterConfiguration _clusterConfiguration;
        private readonly ResourceEventListener _resourceListener = new ResourceEventListener();

        // Hostnames seen by BalanceAsync callers - the sampler will poll connection counts for these.
        private readonly ConcurrentDictionary<string, byte> _observedHosts = new();
        private readonly ConcurrentDictionary<string, int> _hostConnectionCounts = new();

        // ---- Hot-path readable state (only field read by callers) ----
        private volatile ResourceState _currentState = ResourceState.Cool;

        private double _latestMemoryMB;
        private double _latestCPUPercentage;

        // ---- Sampler-owned state (mutated only inside the sampler loop) ----
        private bool _isCoolingStarted;
        private bool _isCoolingPaused;
        private readonly Stopwatch _maxCoolingStopwatch = new Stopwatch();
        private readonly Stopwatch _resetToCoolingStopwatch = new Stopwatch();
        private DateTime _lastGcUtc = DateTime.MinValue;

        // ---- Sampler lifecycle ----
        private readonly CancellationTokenSource _samplerCts = new CancellationTokenSource();
        private readonly Task _samplerTask;
        private TaskCompletionSource<bool> _nextSampleSignal = CreateSampleSignal();

        public double MaxMemoryMB { get; }
        public double MaxCPUPercentage { get; }
        public double CoolDownMemoryMB { get; }
        public double CoolDownCPUPercentage { get; }
        public int MaxConcurrentConnectionsCountPerHostName { get; }
        public int CoolDownConcurrentConnectionsCountPerHostName { get; }
        public int CoolDownRetryTimeInSeconds { get; }
        public int MaxCoolingPeriod { get; }
        public int ResumeCoolingAfter { get; }
        public SuspensionMode SuspensionMode { get; }

        private GrpcMetricsQueryServiceClient _grpcClient;

        public Watchdog(
            double memoryLimitMB,
            double cpuLimit,
            double coolDownMemoryMB,
            double coolDownCPUPercentage,
            int maxConcurrentConnectionsPerHostName,
            int coolDownConcurrentConnectionsCountPerHostName,
            int coolDownRetryTimeInSeconds,
            int maxCoolingPeriod,
            int resumeCoolingAfter,
            SuspensionMode suspensionMode,
            ILogger logger,
            IRuntimeOperationIdProvider operationIdProvider,
            ICustomGrpcClientFactory customGrpcClientFactory,
            IClusterConfiguration clusterConfiguration)
        {
            MaxMemoryMB = memoryLimitMB;
            MaxCPUPercentage = cpuLimit;
            CoolDownMemoryMB = coolDownMemoryMB;
            CoolDownCPUPercentage = coolDownCPUPercentage;
            MaxConcurrentConnectionsCountPerHostName = maxConcurrentConnectionsPerHostName;
            CoolDownConcurrentConnectionsCountPerHostName = coolDownConcurrentConnectionsCountPerHostName;
            CoolDownRetryTimeInSeconds = coolDownRetryTimeInSeconds;
            MaxCoolingPeriod = maxCoolingPeriod;
            ResumeCoolingAfter = resumeCoolingAfter;
            SuspensionMode = suspensionMode;

            _logger = logger;
            _operationIdProvider = operationIdProvider;
            _customGrpcClientFactory = customGrpcClientFactory;
            _clusterConfiguration = clusterConfiguration;
            _grpcClient = customGrpcClientFactory.GetClient<GrpcMetricsQueryServiceClient>(clusterConfiguration.MasterNodeIP);

            _samplerTask = Task.Run(() => SamplerLoopAsync(_samplerCts.Token));
        }

        public static Watchdog GetDefaultInstance(ILogger logger, IRuntimeOperationIdProvider operationIdProvider, ICustomGrpcClientFactory customGrpcClientFactory, IClusterConfiguration clusterConfiguration)
        {
            return new Watchdog(
                1000, 50, 500, 30, 1000, 100, 1, 60, 300,
                SuspensionMode.Any, logger, operationIdProvider, customGrpcClientFactory, clusterConfiguration);
        }

        /// <summary>
        /// Registers the host for sampling and waits until the host can proceed under a Cool state.
        /// </summary>
        public async ValueTask<ResourceState> BalanceAsync(string hostName, CancellationToken token = default)
        {
            if (!string.IsNullOrEmpty(hostName))
            {
                _observedHosts.TryAdd(hostName, 0);
            }

            while (!token.IsCancellationRequested)
            {
                if (EvaluateSnapshot(hostName) == ResourceState.Cool)
                {
                    return ResourceState.Cool;
                }

                Task sampleSignal;
                sampleSignal = _nextSampleSignal.Task;


                await sampleSignal.WaitAsync(token).ConfigureAwait(false);
            }

            token.ThrowIfCancellationRequested();
            return ResourceState.Unknown;
        }

        // -----------------------------------------------------------------------------------------
        // Sampler
        // -----------------------------------------------------------------------------------------

        private async Task SamplerLoopAsync(CancellationToken token)
        {
            var interval = TimeSpan.FromSeconds(CoolDownRetryTimeInSeconds);

            while (!token.IsCancellationRequested)
            {
                try
                {
                    await SampleOnceAsync(token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    try
                    {
                        await _logger.LogAsync(_operationIdProvider.OperationId,
                            $"Watchdog sampler iteration failed.\n{ex}", LPSLoggingLevel.Error, token).ConfigureAwait(false);
                    }
                    catch { /* swallow logger failures inside sampler */ }

                    // On unknown failure, surface as Unknown so callers stop waiting.
                    SetState(ResourceState.Unknown);
                }

                SignalSampleAvailable();

                try
                {
                    await Task.Delay(interval, token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested)
                {
                    break;
                }
            }
        }

        private async Task SampleOnceAsync(CancellationToken token)
        {
            // ----- Sample inputs -----
            double memoryMB = _resourceListener.MemoryUsageMB;
            double cpuPct = _resourceListener.CPUPercentage;
            Volatile.Write(ref _latestMemoryMB, memoryMB);
            Volatile.Write(ref _latestCPUPercentage, cpuPct);


            int hostsExceeded = 0;
            int hostsCooldown = 0;
            int hostCount = 0;
            foreach (var host in _observedHosts.Keys)
            {
                int active = await GetHostActiveConnectionsCountAsync(host).ConfigureAwait(false);
                if (active < 0) continue; // failure already logged
                _hostConnectionCounts[host] = active;
                hostCount++;
                if (active > MaxConcurrentConnectionsCountPerHostName) hostsExceeded++;
                if (active > CoolDownConcurrentConnectionsCountPerHostName) hostsCooldown++;
            }


            bool memExceeded = memoryMB > MaxMemoryMB;
            bool cpuExceeded = cpuPct >= MaxCPUPercentage;
            bool memCooldown = memoryMB > CoolDownMemoryMB;
            bool cpuCooldown = cpuPct >= CoolDownCPUPercentage;

            bool hot, cooling;
            if (SuspensionMode == SuspensionMode.All)
            {
                hot = memExceeded && cpuExceeded && (hostCount > 0 && hostsExceeded == hostCount);
                cooling = memCooldown && cpuCooldown && (hostCount > 0 && hostsCooldown == hostCount);
            }
            else
            {
                hot = memExceeded || cpuExceeded || (hostsExceeded > 0);
                cooling = memCooldown || cpuCooldown || (hostsCooldown > 0);
            }

            // ----- Resume cooling if pause window elapsed -----
            if (_isCoolingPaused && _resetToCoolingStopwatch.Elapsed.TotalSeconds > ResumeCoolingAfter)
            {
                _isCoolingPaused = false;
                _resetToCoolingStopwatch.Reset();
                await _logger.LogAsync(_operationIdProvider.OperationId, "Watchdog: resuming cooling evaluation", LPSLoggingLevel.Information, token).ConfigureAwait(false);
            }

            // ----- Decide next state -----

            ResourceState next;
            if (_isCoolingPaused)
            {
                // Cooling paused -> let traffic flow regardless of pressure.
                next = ResourceState.Cool;
            }
            else if (hot)
            {
                next = ResourceState.Hot;
            }
            else if (cooling && _currentState != ResourceState.Cool)
            {
                next = ResourceState.Cooling;
            }
            else
            {
                next = ResourceState.Cool;
            }

            // ----- Manage cooling lifecycle -----
            if (next != ResourceState.Cool)
            {
                if (!_isCoolingStarted)
                {
                    _isCoolingStarted = true;
                    _maxCoolingStopwatch.Restart();
                    await _logger.LogAsync(_operationIdProvider.OperationId, "Watchdog: cooling has started", LPSLoggingLevel.Information, token).ConfigureAwait(false);
                }

                if (_maxCoolingStopwatch.Elapsed.TotalSeconds > MaxCoolingPeriod)
                {
                    _isCoolingPaused = true;
                    _resetToCoolingStopwatch.Restart();
                    _maxCoolingStopwatch.Reset();
                    _isCoolingStarted = false;
                    await _logger.LogAsync(_operationIdProvider.OperationId,
                        $"Watchdog: max cooling period reached - pausing cooling for {ResumeCoolingAfter}s", LPSLoggingLevel.Warning, token).ConfigureAwait(false);
                    next = ResourceState.Cool;
                }
                else
                {
                    // Throttled, pressure-gated GC.
                    if (memExceeded && (DateTime.UtcNow - _lastGcUtc).TotalSeconds >= GcMinIntervalSeconds)
                    {
                        _lastGcUtc = DateTime.UtcNow;
                        GC.Collect(2, GCCollectionMode.Aggressive, blocking: true, compacting: true);
                        await _logger.LogAsync(_operationIdProvider.OperationId,
                            "Watchdog: full GC executed under memory pressure", LPSLoggingLevel.Warning, token).ConfigureAwait(false);
                    }

                    await _logger.LogAsync(_operationIdProvider.OperationId,
                        $"Watchdog: pressure detected (mem={memoryMB:F0}MB cpu={cpuPct:F0}% state={next})",
                        LPSLoggingLevel.Warning, token).ConfigureAwait(false);
                }
            }
            else
            {
                if (_isCoolingStarted)
                {
                    _isCoolingStarted = false;
                    _maxCoolingStopwatch.Reset();
                }
            }

            SetState(next);
        }

        private ResourceState EvaluateSnapshot(string hostName)
        {
            if (string.IsNullOrWhiteSpace(hostName))
            {
                return _currentState;
            }

            if (!_hostConnectionCounts.TryGetValue(hostName, out int activeRequests))
            {
                return _currentState;
            }

            double memoryMB = Volatile.Read(ref _latestMemoryMB);
            double cpuPct = Volatile.Read(ref _latestCPUPercentage);

            bool memoryExceeded = memoryMB > MaxMemoryMB;
            bool cpuExceeded = cpuPct >= MaxCPUPercentage;
            bool memoryCooldown = memoryMB > CoolDownMemoryMB;
            bool cpuCooldown = cpuPct >= CoolDownCPUPercentage;

            bool hot, cooling;
            if (SuspensionMode == SuspensionMode.All)
            {
                hot = memoryExceeded && cpuExceeded && (activeRequests > MaxConcurrentConnectionsCountPerHostName);
                cooling = memoryCooldown && cpuCooldown && (activeRequests > CoolDownConcurrentConnectionsCountPerHostName);
            }
            else
            {
                hot = memoryExceeded || cpuExceeded || (activeRequests > MaxConcurrentConnectionsCountPerHostName);
                cooling = memoryCooldown || cpuCooldown || (activeRequests > CoolDownConcurrentConnectionsCountPerHostName);
            }

            if (hot)
                return ResourceState.Hot;
            if (cooling)
                return ResourceState.Cooling;
            return ResourceState.Cool;
        }

        private static TaskCompletionSource<bool> CreateSampleSignal()
            => new(TaskCreationOptions.RunContinuationsAsynchronously);

        private void SignalSampleAvailable()
        {
            _nextSampleSignal.TrySetResult(true);
            _nextSampleSignal = CreateSampleSignal();
        }

        /// <summary>
        /// Publishes the new state. Single-writer (sampler only).
        /// </summary>
        private void SetState(ResourceState next)
        {
            _currentState = next;
        }

        private async Task<int> GetHostActiveConnectionsCountAsync(string hostName)
        {
            _grpcClient = _customGrpcClientFactory.GetClient<GrpcMetricsQueryServiceClient>(_clusterConfiguration.MasterNodeIP);
            try
            {
                var request = new MetricRequest
                {
                    Hostname = hostName,
                    Mode = FilterMode.And
                };

                var response = await _grpcClient.GetThroughputMetricsAsync(request);
                return response.Responses.Sum(r => r.CurrentActiveRequests);
            }
            catch (Exception ex)
            {
                await _logger.LogAsync(_operationIdProvider.OperationId,
                    $"Failed to get active connections count.\n{ex}", LPSLoggingLevel.Error);
                return -1;
            }
        }

        public async ValueTask DisposeAsync()
        {
            try
            {
                _samplerCts.Cancel();
                try { await _samplerTask.ConfigureAwait(false); } catch { /* ignore */ }
            }
            finally
            {
                _samplerCts.Dispose();
                _nextSampleSignal.TrySetResult(true);
            }
        }
    }
}
