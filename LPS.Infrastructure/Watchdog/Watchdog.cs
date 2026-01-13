using System;
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
    public class Watchdog : IWatchdog
    {
        private readonly ILogger _logger;
        private readonly IRuntimeOperationIdProvider _operationIdProvider;
        private readonly ICustomGrpcClientFactory _customGrpcClientFactory;
        IClusterConfiguration _clusterConfiguration;
        private readonly ResourceEventListener _resourceListener = new ResourceEventListener();
        private readonly SemaphoreSlim _semaphoreSlim = new SemaphoreSlim(1, 1);

        private ResourceState _resourceState = ResourceState.Cool;
        private bool _isResourceUsageExceeded;
        private bool _isResourceCoolingDown;
        private bool _isCoolingStarted = false;
        private bool _isGCExecuted = false;
        private bool _isCoolingPaused = false;

        private readonly Stopwatch _maxCoolingStopwatch = new Stopwatch();
        private readonly Stopwatch _resetToCoolingStopwatch = new Stopwatch();

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
        GrpcMetricsQueryServiceClient _grpcClient;
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
        }

        public static Watchdog GetDefaultInstance(ILogger logger, IRuntimeOperationIdProvider operationIdProvider, ICustomGrpcClientFactory customGrpcClientFactory, IClusterConfiguration clusterConfiguration)
        {
            return new Watchdog(
                1000, 50, 500, 30, 1000, 100, 1, 60, 300,
                SuspensionMode.Any, logger, operationIdProvider, customGrpcClientFactory, clusterConfiguration);
        }

        public async Task<ResourceState> BalanceAsync(string hostName, CancellationToken token = default)
        {
            bool acquired = false;

            try
            {
                await _semaphoreSlim.WaitAsync(token);
                acquired = true;

                if (_isCoolingPaused && _resetToCoolingStopwatch.Elapsed.TotalSeconds > ResumeCoolingAfter)
                {
                    await _logger.LogAsync(_operationIdProvider.OperationId, "Resuming cooling if needed", LPSLoggingLevel.Information, token);
                    _resetToCoolingStopwatch.Reset();
                    _isCoolingPaused = false;
                    await _logger.LogAsync(_operationIdProvider.OperationId, "Resuming cooling if needed", LPSLoggingLevel.Information, token);
                }

                await UpdateResourceUsageFlagAsync(hostName);
                await UpdateResourceCoolingFlagAsync(hostName);
                _resourceState = DetermineResourceState();

                while (_resourceState != ResourceState.Cool && !_isCoolingPaused && !token.IsCancellationRequested)
                {
                    if (!_isCoolingStarted) await StartCoolingAsync(token);
                    if (_maxCoolingStopwatch.Elapsed.TotalSeconds > MaxCoolingPeriod)
                    {
                        await PauseCoolingAsync(token);
                        break;
                    }

                    if (!_isGCExecuted) await ExecuteGarbageCollectionAsync(token);

                    await LogCoolingInitiationAsync(token);
                    await Task.Delay(TimeSpan.FromSeconds(CoolDownRetryTimeInSeconds), token);

                    await UpdateResourceUsageFlagAsync(hostName);
                    await UpdateResourceCoolingFlagAsync(hostName);
                    _resourceState = DetermineResourceState();
                }
            }
            catch (Exception ex)
            {
                await _logger.LogAsync(_operationIdProvider.OperationId, $"Watchdog failed to balance resources.\n{ex}", LPSLoggingLevel.Error, token);
                _resourceState = ResourceState.Unknown;
            }
            finally
            {
                ResetCoolingState();
                if (acquired) _semaphoreSlim.Release();
            }

            return _resourceState;
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

                int totalActiveConnections = response.Responses.Sum(r => r.CurrentActiveRequests);
                return totalActiveConnections;
            }
            catch (Exception ex)
            {
                await _logger.LogAsync(_operationIdProvider.OperationId,
                    $"Failed to get active connections count.\n{ex}", LPSLoggingLevel.Error);
                return -1;
            }
        }

        private async Task UpdateResourceUsageFlagAsync(string hostName)
        {
            bool memoryExceeded = _resourceListener.MemoryUsageMB > MaxMemoryMB;
            bool cpuExceeded = _resourceListener.CPUPercentage >= MaxCPUPercentage;
            bool connectionsExceeded = (await GetHostActiveConnectionsCountAsync(hostName)) > MaxConcurrentConnectionsCountPerHostName;

            _isResourceUsageExceeded = SuspensionMode switch
            {
                SuspensionMode.Any => memoryExceeded || cpuExceeded || connectionsExceeded,
                SuspensionMode.All => memoryExceeded && cpuExceeded && connectionsExceeded,
                _ => false
            };
        }

        private async Task UpdateResourceCoolingFlagAsync(string hostName)
        {
            bool memoryExceedsCooldown = _resourceListener.MemoryUsageMB > CoolDownMemoryMB;
            bool cpuExceedsCooldown = _resourceListener.CPUPercentage >= CoolDownCPUPercentage;
            bool connectionsExceedsCooldown = (await GetHostActiveConnectionsCountAsync(hostName)) > CoolDownConcurrentConnectionsCountPerHostName;

            bool coolingCondition = SuspensionMode switch
            {
                SuspensionMode.Any => memoryExceedsCooldown || cpuExceedsCooldown || connectionsExceedsCooldown,
                SuspensionMode.All => memoryExceedsCooldown && cpuExceedsCooldown && connectionsExceedsCooldown,
                _ => false
            };

            _isResourceCoolingDown = coolingCondition && _resourceState != ResourceState.Cool;
        }

        private ResourceState DetermineResourceState()
        {
            return _isResourceUsageExceeded
                ? ResourceState.Hot
                : _isResourceCoolingDown
                    ? ResourceState.Cooling
                    : ResourceState.Cool;
        }

        private async Task StartCoolingAsync(CancellationToken token)
        {
            await _logger.LogAsync(_operationIdProvider.OperationId, "Cooling has started", LPSLoggingLevel.Information, token);
            _isCoolingStarted = true;
            _maxCoolingStopwatch.Start();
        }

        private async Task PauseCoolingAsync(CancellationToken token)
        {
            _isCoolingPaused = true;
            _resetToCoolingStopwatch.Start();
            await _logger.LogAsync(_operationIdProvider.OperationId, $"Pausing cooling for {ResumeCoolingAfter} seconds", LPSLoggingLevel.Information, token);
        }

        private async Task ExecuteGarbageCollectionAsync(CancellationToken token)
        {
            GC.Collect();
            _isGCExecuted = true;
            await _logger.LogAsync(_operationIdProvider.OperationId, "Garbage collection executed", LPSLoggingLevel.Warning, token);
        }

        private async Task LogCoolingInitiationAsync(CancellationToken token)
        {
            await _logger.LogAsync(_operationIdProvider.OperationId, "Resource utilization limit reached - initiating cooling...", LPSLoggingLevel.Warning, token);
        }

        private void ResetCoolingState()
        {
            _maxCoolingStopwatch.Reset();
            _isCoolingStarted = false;
            _isGCExecuted = false;
        }
    }
}
