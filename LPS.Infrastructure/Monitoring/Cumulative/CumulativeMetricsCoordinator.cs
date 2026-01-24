#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;

namespace LPS.Infrastructure.Monitoring.Cumulative
{
    /// <summary>
    /// Coordinator that manages cumulative metrics timing. Fires an event at regular intervals.
    /// Cumulative collectors subscribe to this event to push their data.
    /// </summary>
    public interface ICumulativeMetricsCoordinator : IDisposable
    {
        /// <summary>
        /// Event fired when it's time to push cumulative metrics.
        /// </summary>
        event Action? OnPushInterval;


        /// <summary>
        /// Start the coordinator timer (async).
        /// </summary>
        ValueTask StartAsync(CancellationToken token);

        /// <summary>
        /// Stop the coordinator timer (async).
        /// </summary>
        ValueTask StopAsync(CancellationToken token);

        /// <summary>
        /// Push interval in milliseconds.
        /// </summary>
        int IntervalMs { get; }

        /// <summary>
        /// Whether the coordinator is running.
        /// </summary>
        bool IsRunning { get; }
    }

    /// <summary>
    /// Timer-based coordinator that fires push events at regular intervals for cumulative metrics.
    /// </summary>
    public sealed class CumulativeMetricsCoordinator : ICumulativeMetricsCoordinator
    {
        private Timer? _timer;
        private bool _isRunning;
        private bool _disposed;

        public event Action? OnPushInterval;

        public int IntervalMs { get; }
        public bool IsRunning => _isRunning;

        public CumulativeMetricsCoordinator(int intervalSeconds = 3)
        {
            IntervalMs = intervalSeconds * 1000;
        }

        public CumulativeMetricsCoordinator(TimeSpan interval)
        {
            IntervalMs = (int)interval.TotalMilliseconds;
        }


        public async ValueTask StartAsync(CancellationToken token)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(CumulativeMetricsCoordinator));
            if (_isRunning) return;

            _isRunning = true;
            _timer = new Timer(
                OnTimerTick,
                null,
                IntervalMs,
                IntervalMs); 
            await ValueTask.CompletedTask;
        }

        public async ValueTask StopAsync(CancellationToken token)
        {
            if (!_isRunning) return;
            _timer?.Change(Timeout.Infinite, Timeout.Infinite);

            // Fire one final event so collectors can push final state
            try
            {
                OnPushInterval?.Invoke();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
            finally
            {
                _isRunning = false;
            }
            await ValueTask.CompletedTask;
        }

        private void OnTimerTick(object? state)
        {
            if (!_isRunning) return;

            try
            {
                OnPushInterval?.Invoke();
            }
            catch
            {
                // Swallow exceptions from handlers to prevent timer from dying
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _isRunning = false;
            _timer?.Dispose();
            _timer = null;
        }
    }
}
