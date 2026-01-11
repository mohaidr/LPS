using System;
using LPS.Infrastructure.Common.Interfaces;

namespace LPS.Infrastructure.Monitoring.MetricsServices
{
    /// <summary>
    /// Thread-safe singleton that holds plan-level execution context.
    /// Only one plan runs per process at a time, so simple locking is sufficient.
    /// </summary>
    public sealed class PlanExecutionContext : IPlanExecutionContext
    {
        private readonly object _lock = new();
        private string _planName = "unknown";
        private DateTime _testStartTime = DateTime.UtcNow;

        public string PlanName
        {
            get
            {
                lock (_lock)
                {
                    return _planName;
                }
            }
        }

        public DateTime TestStartTime
        {
            get
            {
                lock (_lock)
                {
                    return _testStartTime;
                }
            }
        }

        public void SetContext(string planName, DateTime testStartTime)
        {
            lock (_lock)
            {
                _planName = string.IsNullOrWhiteSpace(planName) ? "unknown" : planName;
                _testStartTime = testStartTime;
            }
        }

        public void Clear()
        {
            lock (_lock)
            {
                _planName = "unknown";
                _testStartTime = DateTime.UtcNow;
            }
        }
    }
}
