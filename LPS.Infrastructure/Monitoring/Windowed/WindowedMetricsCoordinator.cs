#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;

namespace LPS.Infrastructure.Monitoring.Windowed
{
    /// <summary>
    /// Coordinator that manages window timing. Fires an event when each window closes.
    /// Aggregators subscribe to this event to know when to snapshot and push to queue.
    /// </summary>
    public interface IWindowedMetricsCoordinator : IDisposable
    {
        /// <summary>
        /// Event fired when a window closes. Subscribers should snapshot their data,
        /// push to queue, and reset.
        /// </summary>
        event Action? OnWindowClosed;

        /// <summary>
        /// Start the coordinator timer.
        /// </summary>
        void Start();

        /// <summary>
        /// Stop the coordinator timer.
        /// </summary>
        void Stop();

        /// <summary>
        /// Start the coordinator timer (async).
        /// </summary>
        ValueTask StartAsync(CancellationToken token);

        /// <summary>
        /// Stop the coordinator timer (async).
        /// </summary>
        ValueTask StopAsync(CancellationToken token);

        /// <summary>
        /// Current window sequence number.
        /// </summary>
        int WindowSequence { get; }

        /// <summary>
        /// Window interval in milliseconds.
        /// </summary>
        int WindowIntervalMs { get; }

        /// <summary>
        /// Whether the coordinator is running.
        /// </summary>
        bool IsRunning { get; }
    }

    /// <summary>
    /// Timer-based coordinator that fires window close events at regular intervals.
    /// </summary>
    public sealed class WindowedMetricsCoordinator : IWindowedMetricsCoordinator
    {
        private Timer? _timer;
        private int _windowSequence;
        private bool _isRunning;
        private bool _disposed;

        public event Action? OnWindowClosed;

        public int WindowIntervalMs { get; }
        public int WindowSequence => _windowSequence;
        public bool IsRunning => _isRunning;

        public WindowedMetricsCoordinator(int windowIntervalSeconds = 5)
        {
            WindowIntervalMs = windowIntervalSeconds * 1000;
        }

        public WindowedMetricsCoordinator(TimeSpan windowInterval)
        {
            WindowIntervalMs = (int)windowInterval.TotalMilliseconds;
        }

        public void Start()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(WindowedMetricsCoordinator));
            if (_isRunning) return;

            _isRunning = true;
            _timer = new Timer(
                OnTimerTick,
                null,
                WindowIntervalMs,
                WindowIntervalMs);
        }

        public void Stop()
        {
            if (!_isRunning) return;
            _isRunning = false;
            _timer?.Change(Timeout.Infinite, Timeout.Infinite);
            
            // Fire one final event so collectors can push final state
            Interlocked.Increment(ref _windowSequence);
            try { OnWindowClosed?.Invoke(); }
            catch { /* Swallow */ }
        }

        public ValueTask StartAsync(CancellationToken token)
        {
            Start();
            return ValueTask.CompletedTask;
        }

        public ValueTask StopAsync(CancellationToken token)
        {
            Stop();
            return ValueTask.CompletedTask;
        }

        private void OnTimerTick(object? state)
        {
            if (!_isRunning) return;

            Interlocked.Increment(ref _windowSequence);
            
            try
            {
                OnWindowClosed?.Invoke();
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
