using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using HtmlAgilityPack;
using LPS.Domain.Common.Interfaces;
using LPS.Infrastructure.Logger;
using LPS.Infrastructure.Monitoring.EventListeners;
using LPS.Infrastructure.Monitoring.Metrics;
using Microsoft.Extensions.Logging;

namespace LPS.Infrastructure.Watchdog
{
    public enum SuspensionMode
    {
        Any,
        All
    }
    public class LPSWatchdog : ILPSWatchdog
    {
        private static SemaphoreSlim _semaphoreSlim = new SemaphoreSlim(1, 1);
        public double MaxMemoryMB { get { return _maxMemoryMB; } }
        public double MaxCPUPercentage { get { return _maxCPUPercentage; } }
        public double CoolDownMemoryMB { get { return _coolDownMemoryMB; } }
        public double CoolDownCPUPercentage { get { return _coolDownCPUPercentage; } }
        public SuspensionMode SuspensionMode { get { return _suspensionMode; } }
        public int CoolDownRetryTimeInSeconds { get { return _coolDownRetryTimeInSeconds; } }
        public int MaxConcurrentConnectionsCountPerHostName { get { return _maxConcurrentConnectionsCountPerHostName; } }
        public int CoolDownConcurrentConnectionsCountPerHostName { get { return _coolDownConcurrentConnectionsCountPerHostName; } }

        private double _maxMemoryMB;
        private double _maxCPUPercentage;
        private double _coolDownMemoryMB;
        private double _coolDownCPUPercentage;
        private int _coolDownRetryTimeInSeconds;
        private int _maxConcurrentConnectionsCountPerHostName;
        private int _coolDownConcurrentConnectionsCountPerHostName;
        private ResourceState _resourceState;
        private SuspensionMode _suspensionMode;

        private LPSResourceEventListener _lpsResourceListener;
        ILPSLogger _logger;
        ILPSRuntimeOperationIdProvider _runtimeOperationIdProvider;
        private LPSWatchdog(ILPSLogger logger, ILPSRuntimeOperationIdProvider runtimeOperationIdProvider)
        {
            _maxCPUPercentage = 50;
            _maxMemoryMB = 1000;
            _coolDownMemoryMB = 500;
            _coolDownCPUPercentage = 30;
            _suspensionMode = SuspensionMode.Any;
            _maxConcurrentConnectionsCountPerHostName = 1000;
            _coolDownConcurrentConnectionsCountPerHostName = 100;
            _coolDownRetryTimeInSeconds = 1;
            _resourceState = ResourceState.Cool;
            _lpsResourceListener = new LPSResourceEventListener();
            _logger = logger;
            _runtimeOperationIdProvider = runtimeOperationIdProvider;
        }

        public LPSWatchdog(double memoryLimitMB,
            double cpuLimit,
            double coolDownMemoryMB,
            double coolDownCPUPercentage,
            int maxConcurrentConnectionsPerHostName,
            int coolDownConcurrentConnectionsCountPerHostName,
            int coolDownRetryTimeInSeconds,
            SuspensionMode suspensionMode, ILPSLogger logger, ILPSRuntimeOperationIdProvider runtimeOperationIdProvider)
        {
            _maxCPUPercentage = cpuLimit;
            _maxMemoryMB = memoryLimitMB;
            _coolDownMemoryMB = coolDownMemoryMB;
            _coolDownCPUPercentage = coolDownCPUPercentage;
            _suspensionMode = suspensionMode;
            _maxConcurrentConnectionsCountPerHostName = maxConcurrentConnectionsPerHostName;
            _coolDownConcurrentConnectionsCountPerHostName = coolDownConcurrentConnectionsCountPerHostName;
            _coolDownRetryTimeInSeconds = coolDownRetryTimeInSeconds;
            _resourceState = ResourceState.Cool;
            _lpsResourceListener = new LPSResourceEventListener();
            _logger = logger;
            _runtimeOperationIdProvider = runtimeOperationIdProvider;
        }
        bool _isResourceUsageExceeded;
        bool _isResourceCoolingDown;

        private void UpdateResourceUsageFlag(string hostName)
        {
            bool memoryExceededTheLimit = _lpsResourceListener.MemoryUsageMB > _maxMemoryMB;
            bool cpuExceededTheLimit = _lpsResourceListener.CPUPercentage >= _maxCPUPercentage;
            bool hostActiveConnectionsExceededTheLimit = GetHostActiveConnectionsCount(hostName) > _maxConcurrentConnectionsCountPerHostName;
            switch (_suspensionMode)
            {
                case SuspensionMode.Any:
                    _isResourceUsageExceeded = memoryExceededTheLimit
                        || cpuExceededTheLimit
                        || hostActiveConnectionsExceededTheLimit;
                    break;
                case SuspensionMode.All:
                    _isResourceUsageExceeded = memoryExceededTheLimit
                        && cpuExceededTheLimit
                        && hostActiveConnectionsExceededTheLimit
                        ;
                    break;
            }
        }

        private void UpdateResourceCoolingFlag(string hostName)
        {
            bool memoryExceedsTheCoolingLimit = _lpsResourceListener.MemoryUsageMB > _coolDownMemoryMB;
            bool cpuExceedsTheCPULimit = _lpsResourceListener.CPUPercentage >= _coolDownCPUPercentage;
            bool hostActiveConnectionsExceedsTheConnectionsLimit = GetHostActiveConnectionsCount(hostName) > _coolDownConcurrentConnectionsCountPerHostName;

            switch (_suspensionMode)
            {
                case SuspensionMode.All:
                    _isResourceCoolingDown = (memoryExceedsTheCoolingLimit
                        && cpuExceedsTheCPULimit
                        && hostActiveConnectionsExceedsTheConnectionsLimit)
                        && _resourceState != ResourceState.Cool;
                    break;
                case SuspensionMode.Any:
                    _isResourceCoolingDown = (memoryExceedsTheCoolingLimit
                        || cpuExceedsTheCPULimit
                        || hostActiveConnectionsExceedsTheConnectionsLimit)
                        && _resourceState != ResourceState.Cool;
                    break;
            }

        }
        
        public async Task<ResourceState> BalanceAsync(string hostName, ICancellationTokenWrapper cancellationTokenWrapper)
        {
            try
            {
                UpdateResourceUsageFlag(hostName);
                UpdateResourceCoolingFlag(hostName);
                _resourceState = _isResourceUsageExceeded ? ResourceState.Hot : _isResourceCoolingDown ? ResourceState.Cooling : ResourceState.Cool;
                while (_resourceState != ResourceState.Cool)
                {
                    await Task.Delay(_coolDownRetryTimeInSeconds * 1000);
                    UpdateResourceUsageFlag(hostName);
                    UpdateResourceCoolingFlag(hostName);
                    _resourceState = _isResourceUsageExceeded ? ResourceState.Hot : _isResourceCoolingDown ? ResourceState.Cooling : ResourceState.Cool;
                }
            }
            catch (Exception ex) 
            {
                _logger.Log(_runtimeOperationIdProvider.OperationId, $"Watchdog has failed to balance the resource usage.\n{ex.Message}\n{ex.InnerException?.Message}\n{ex.StackTrace}", LPSLoggingLevel.Error);
                _resourceState = ResourceState.Unkown;
            }
            return _resourceState;
        }

        private int GetHostActiveConnectionsCount(string hostName)
        {
            try
            {
                int hostActiveConnectionsCount = LPSMetricsDataSource
                    .Get<LPSConnectionsMetricGroup>(metric => metric.GetDimensionSet<ConnectionDimensionSet>()?.EndPointDetails!= null && metric.GetDimensionSet<ConnectionDimensionSet>().EndPointDetails.Contains(hostName))
                    .Select(metric => metric.GetDimensionSet<ConnectionDimensionSet>().ActiveRequestsCount)
                    .Sum();
                return hostActiveConnectionsCount;
            }
            catch (Exception ex) {
                _logger.Log(_runtimeOperationIdProvider.OperationId, $"Failed to get the active connections count \n{ex.Message}\n{ex.InnerException?.Message}\n{ex.StackTrace}", LPSLoggingLevel.Error);
                return -1;
            }
         
        }

        public static LPSWatchdog GetDefaultInstance(ILPSLogger logger, ILPSRuntimeOperationIdProvider runtimeOperationIdProvider)
        {
            return new LPSWatchdog(logger, runtimeOperationIdProvider);
        }

    }
}
