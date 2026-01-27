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

        public async Task<bool> UpdateAsync(bool isBreachingNow)
        {
            await _semaphore.WaitAsync();
            try
            {
                if (isBreachingNow)
                    _breachStartUtc ??= DateTime.UtcNow;
                else
                    _breachStartUtc = null;

                return _breachStartUtc != null &&
                    (DateTime.UtcNow - _breachStartUtc) >= _gracePeriod;
            }
            finally
            {
                _semaphore.Release();
            }
        }
    }
}
