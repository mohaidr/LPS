#nullable enable
using System;
using System.Linq;
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
        /// push to queue, and reset. Handlers return Task to allow awaiting completion.
        /// </summary>
        event Func<Task>? OnWindowClosed;

        /// <summary>
        /// Start the coordinator timer (async).
        /// </summary>
        ValueTask StartAsync(CancellationToken token);

        /// <summary>
        /// Stop the coordinator timer (async).
        /// </summary>
        ValueTask StopAsync(CancellationToken token);

        /// <summary>
        /// Flush all collectors by invoking OnWindowClosed and awaiting all handlers.
        /// Use this to ensure all data is pushed before draining the queue.
        /// </summary>
        Task FlushAllAsync();

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

        public event Func<Task>? OnWindowClosed;

        public int WindowIntervalMs { get; }
        public int WindowSequence => _windowSequence;
        public bool IsRunning => _isRunning;

        public WindowedMetricsCoordinator(int windowIntervalSeconds = 3)
        {
            WindowIntervalMs = windowIntervalSeconds * 1000;
        }

        public WindowedMetricsCoordinator(TimeSpan windowInterval)
        {
            WindowIntervalMs = (int)windowInterval.TotalMilliseconds;
        }



        public async ValueTask StartAsync(CancellationToken token)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(WindowedMetricsCoordinator));
            if (_isRunning) return;

            _isRunning = true;
            _timer = new Timer(
                OnTimerTick,
                null,
                WindowIntervalMs,
                WindowIntervalMs);
            await ValueTask.CompletedTask;
        }

        public async ValueTask StopAsync(CancellationToken token)
        {
            if (!_isRunning) return;
            _timer?.Change(Timeout.Infinite, Timeout.Infinite);

            // Fire one final event so collectors can push final state
            Interlocked.Increment(ref _windowSequence);
            try
            {
                await FlushAllAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
            finally
            {
                _isRunning = false;
            }
        }

        /// <summary>
        /// Flush all collectors by invoking OnWindowClosed and awaiting all handlers.
        /// </summary>
        public async Task FlushAllAsync()
        {
            var handlers = OnWindowClosed?.GetInvocationList();
            if (handlers == null || handlers.Length == 0) return;

            var tasks = handlers.Cast<Func<Task>>().Select(h =>
            {
                try { return h(); }
                catch { return Task.CompletedTask; }
            });
            await Task.WhenAll(tasks);
        }

        private void OnTimerTick(object? state)
        {
            if (!_isRunning) return;

            Interlocked.Increment(ref _windowSequence);

            try
            {
                // Fire and forget for timer ticks - we can't await in timer callback
                // But handlers are async so they run independently
                _ = FlushAllAsync();
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
