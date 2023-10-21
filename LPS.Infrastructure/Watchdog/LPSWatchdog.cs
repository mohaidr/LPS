using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using HtmlAgilityPack;
using LPS.Domain.Common;

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

        private double _maxMemoryMB;
        private double _maxCPUPercentage;
        private double _coolDownMemoryMB;
        private double _coolDownCPUPercentage;
        private int _coolDownRetryTimeInSeconds;
        private int _maxConnectionsCountPerHostName;
        private int _coolDownConnectionsCountPerHostName;
        private ResourceState _resourceState;
        private SuspensionMode _suspensionMode;

        private LPSResourceEventListener _lpsResourceListener;
        private LPSConnectionCounterEventListener _lpsConnectionsCountEventListener;
        public LPSWatchdog(double memoryLimitMB,
            double cpuLimit,
            double coolDownMemoryMB,
            double coolDownCPUPercentage,
            int maxConnectionsPerHostName,
            int coolDownConnectionsCountPerHostName,
            int coolDownRetryTimeInSeconds,
            SuspensionMode suspensionMode)
        {
            _maxCPUPercentage = cpuLimit;
            _maxMemoryMB = memoryLimitMB;
            _coolDownMemoryMB = coolDownMemoryMB;
            _coolDownCPUPercentage = coolDownCPUPercentage;
            _suspensionMode = suspensionMode;
            _maxConnectionsCountPerHostName = maxConnectionsPerHostName;
            _coolDownConnectionsCountPerHostName = coolDownConnectionsCountPerHostName;
            _coolDownRetryTimeInSeconds = coolDownRetryTimeInSeconds;
            _resourceState = ResourceState.Cool;
            _lpsResourceListener = new LPSResourceEventListener();
            _lpsConnectionsCountEventListener = new LPSConnectionCounterEventListener();
        }
        bool _isResourceUsageExceeded;
        bool _isResourceCoolingDown;
        private void UpdateResourceUsageFlag(string hostName)
        {
            bool memoryExceededTheLimit = _lpsResourceListener.MemoryUsageMB > _maxMemoryMB;
            bool cpuExceededTheLimit = _lpsResourceListener.CPUPercentage >= _maxCPUPercentage;
            bool hostActiveConnectionsExceededTheLimit = _lpsConnectionsCountEventListener.GetHostActiveConnectionsCount(hostName) > _maxConnectionsCountPerHostName;
            switch (_suspensionMode)
            {
                case SuspensionMode.All:
                    _isResourceUsageExceeded = memoryExceededTheLimit
                        && cpuExceededTheLimit
                        && hostActiveConnectionsExceededTheLimit
                        ;
                    break;
                case SuspensionMode.Any:
                    _isResourceUsageExceeded = memoryExceededTheLimit
                        || cpuExceededTheLimit
                        || hostActiveConnectionsExceededTheLimit;
                    break;
            }
        }

        private void UpdateResourceCoolingFlag(string hostName)
        {
            bool memoryExceededTheLimit = _lpsResourceListener.MemoryUsageMB > _coolDownMemoryMB;
            bool cpuExceededTheLimit = _lpsResourceListener.CPUPercentage >= _coolDownCPUPercentage;
            bool hostActiveConnectionsExceededTheLimit = _lpsConnectionsCountEventListener.GetHostActiveConnectionsCount(hostName) > _coolDownConnectionsCountPerHostName;
            switch (_suspensionMode)
            {
                case SuspensionMode.All:
                    _isResourceCoolingDown = (memoryExceededTheLimit
                        && cpuExceededTheLimit
                        && hostActiveConnectionsExceededTheLimit) 
                        && _resourceState != ResourceState.Cool;
                    break;
                case SuspensionMode.Any:
                    _isResourceCoolingDown = (memoryExceededTheLimit
                        || cpuExceededTheLimit
                        || hostActiveConnectionsExceededTheLimit) 
                        && _resourceState != ResourceState.Cool;
                    break;
            }

        }

        public async Task<ResourceState> Balance(string hostName)
        {
            await _semaphoreSlim.WaitAsync();

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

            _semaphoreSlim.Release();

            return _resourceState;
        }
    }
}
