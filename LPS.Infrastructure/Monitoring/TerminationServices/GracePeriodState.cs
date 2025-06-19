using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace LPS.Infrastructure.Monitoring.TerminationServices
{
    internal class GracePeriodState
    {
        private DateTime? _errorTrendStartUtc;
        private readonly TimeSpan _gracePeriod;
        private readonly SemaphoreSlim _semaphore = new(1, 1);

        public GracePeriodState(TimeSpan gracePeriod)
        {
            _gracePeriod = gracePeriod;
        }

        public async Task<bool> UpdateAndCheckAsync(int totalCount, int errorCount, double threshold)
        {
            await _semaphore.WaitAsync();
            try
            {
                if (totalCount == 0)
                {
                    _errorTrendStartUtc = null;
                    return false;
                }

                double errorRate = (double)errorCount / totalCount;

                if (errorRate >= threshold)
                {
                    _errorTrendStartUtc ??= DateTime.UtcNow;
                }
                else
                {
                    _errorTrendStartUtc = null;
                }

                return _errorTrendStartUtc != null &&
                       (DateTime.UtcNow - _errorTrendStartUtc) >= _gracePeriod;
            }
            finally
            {
                _semaphore.Release();
            }
        }
    }
}
