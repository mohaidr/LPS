using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using HtmlAgilityPack;
using LPS.Domain.Common;

namespace LPS.Infrastructure.ResourceUsageTracker
{
    public enum SuspensionMode
    {
        Any,
        All
    }
    public class LPSResourceTracker : ILPSResourceTracker
    {
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
        private ResourceState _resourceState;
        private SuspensionMode _suspensionMode;

        private LPSResourceMonitorService _resourceMonitorService;

        public LPSResourceTracker(double memoryLimitMB,
            double cpuLimit,
            double coolDownMemoryMB,
            double coolDownCPUPercentage,
            int coolDownRetryTimeInSeconds,
            SuspensionMode suspensionMode)
        {
            _maxCPUPercentage = cpuLimit;
            _maxMemoryMB = memoryLimitMB;
            _coolDownMemoryMB = coolDownMemoryMB;
            _coolDownCPUPercentage = coolDownCPUPercentage;
            _suspensionMode = suspensionMode;
            _coolDownRetryTimeInSeconds = coolDownRetryTimeInSeconds;
            _resourceState = ResourceState.Cool;
            _resourceMonitorService = new LPSResourceMonitorService();
        }
        bool _isResourceUsageExceeded;
        bool _isResourceCoolingDown;
        private void UpdateResourceUsageFlag()
        {
            switch (_suspensionMode)
            {
                case SuspensionMode.All:
                    _isResourceUsageExceeded = _resourceMonitorService.MemoryUsageMB >= _maxMemoryMB && _resourceMonitorService.CPUPercentage >= _maxCPUPercentage;
                    break;
                case SuspensionMode.Any:
                    _isResourceUsageExceeded = _resourceMonitorService.MemoryUsageMB >= _maxMemoryMB || _resourceMonitorService.CPUPercentage >= _maxCPUPercentage;
                    break;
            }
        }

        private void UpdateResourceCoolingFlag()
        {
            switch (_suspensionMode)
            {
                case SuspensionMode.All:
                    _isResourceCoolingDown = (_resourceMonitorService.MemoryUsageMB > _coolDownMemoryMB && _resourceMonitorService.CPUPercentage >= _coolDownCPUPercentage) && _resourceState == ResourceState.Hot;
                    break;
                case SuspensionMode.Any:
                    _isResourceCoolingDown = (_resourceMonitorService.MemoryUsageMB >= _coolDownMemoryMB || _resourceMonitorService.CPUPercentage >= _coolDownCPUPercentage) && (_resourceState != ResourceState.Cool);
                    break;
            }
        }

        public ResourceState Balance()
        {
            UpdateResourceUsageFlag();
            if (_isResourceUsageExceeded)
            {
                _resourceState = ResourceState.Hot;
                Console.WriteLine(_resourceState);
            }
            else
            {
                UpdateResourceCoolingFlag();
                if (_isResourceCoolingDown)
                {
                    Console.WriteLine(_resourceState);
                    _resourceState = ResourceState.Cooling;
                }
                else
                {
                    _resourceState = ResourceState.Cool;
                }
            }
            if (_resourceState != ResourceState.Cool)
            {
                Thread.Sleep(_coolDownRetryTimeInSeconds * 1000);
                Console.WriteLine("Going to sleep to balance Resource Usage");
            }
            return _resourceState;
        }
    }
}
