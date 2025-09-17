// GracePeriodState.cs
using System;
using System.Threading;
using System.Threading.Tasks;

namespace LPS.Infrastructure.Monitoring.TerminationServices
{
    internal class GracePeriodState
    {
        private DateTime? _breachStartUtc;
        private readonly TimeSpan _gracePeriod;
        private readonly SemaphoreSlim _semaphore = new(1, 1);

        public GracePeriodState(TimeSpan gracePeriod)
        {
            _gracePeriod = gracePeriod;
        }

        /// <summary>
        /// Grace check for rates computed from counts (e.g., errorRate).
        /// </summary>
        public async Task<bool> UpdateAndCheckRateAsync(int totalCount, int errorCount, double threshold)
        {
            await _semaphore.WaitAsync();
            try
            {
                if (totalCount == 0)
                {
                    _breachStartUtc = null;
                    return false;
                }

                double rate = (double)errorCount / totalCount;
                return UpdateAndCheckCore(rate >= threshold);
            }
            finally
            {
                _semaphore.Release();
            }
        }

        /// <summary>
        /// Grace check for direct values (e.g., P90, P50, P10, Average).
        /// </summary>
        public async Task<bool> UpdateAndCheckValueAsync(double value, double threshold)
        {
            await _semaphore.WaitAsync();
            try
            {
                return UpdateAndCheckCore(value >= threshold);
            }
            finally
            {
                _semaphore.Release();
            }
        }

        private bool UpdateAndCheckCore(bool isBreachingNow)
        {
            if (isBreachingNow)
                _breachStartUtc ??= DateTime.UtcNow;
            else
                _breachStartUtc = null;

            return _breachStartUtc != null &&
                   (DateTime.UtcNow - _breachStartUtc) >= _gracePeriod;
        }
    }
}
