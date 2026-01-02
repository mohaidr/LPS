using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using LPS.Domain.Common.Interfaces;
using LPS.Domain.Domain.Common.Interfaces;

namespace LPS.Domain.LPSRun.LPSHttpIteration.Scheduler
{
    public class HttpIterationSchedulerService : IHttpIterationSchedulerService
    {
        private readonly IMetricsDataMonitor _lpsMetricsDataMonitor;
        private readonly IWatchdog _watchdog;
        private readonly IRuntimeOperationIdProvider _runtimeOperationIdProvider;
        private readonly ILogger _logger;
        private readonly IIterationStatusMonitor _iterationStatusMonitor;
        private readonly IClientManager<HttpRequest, HttpResponse, IClientService<HttpRequest, HttpResponse>> _lpsClientManager;
        private readonly IClientConfiguration<HttpRequest> _lpsClientConfig;

        // Track CancellationTokenSource per iteration for termination-aware cancellation
        private readonly ConcurrentDictionary<Guid, CancellationTokenSource> _iterationCancellationSources = new();
        private readonly ConcurrentDictionary<Guid, Task> _terminationWatchers = new();

        public HttpIterationSchedulerService(
            ILogger logger,
            IWatchdog watchdog,
            IRuntimeOperationIdProvider runtimeOperationIdProvider,
            IMetricsDataMonitor lpsMetricsDataMonitor,
            IIterationStatusMonitor iterationStatusMonitor,
            IClientManager<HttpRequest, HttpResponse, IClientService<HttpRequest, HttpResponse>> lpsClientManager,
            IClientConfiguration<HttpRequest> lpsClientConfig)
        {
            _lpsMetricsDataMonitor = lpsMetricsDataMonitor;
            _watchdog = watchdog;
            _runtimeOperationIdProvider = runtimeOperationIdProvider;
            _iterationStatusMonitor = iterationStatusMonitor;
            _lpsClientManager = lpsClientManager;
            _lpsClientConfig = lpsClientConfig;
            _logger = logger;
        }

        public async Task ScheduleAsync(
            DateTime scheduledTime,
            HttpIteration.ExecuteCommand httpIterationCommand,
            HttpIteration httpIteration,
            CancellationToken token)
        {
            // Get or create termination-aware cancellation for this iteration
            var iterationCts = _iterationCancellationSources.GetOrAdd(
                httpIteration.Id,
                _ => new CancellationTokenSource());

            // Start termination watcher for this iteration (only once per iteration)
            // Fire-and-forget is intentional - watcher runs in background
            _ = _terminationWatchers.GetOrAdd(
                httpIteration.Id,
                _ => StartTerminationWatcherAsync(httpIteration, iterationCts, token));

            // Create linked token: cancels if either original token OR iteration termination
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(token, iterationCts.Token);
            var linkedToken = linkedCts.Token;

            try
            {
                var delayTime = (scheduledTime - DateTime.Now);
                if (delayTime > TimeSpan.Zero)
                {
                    await Task.Delay(delayTime, linkedToken);
                }

                if (httpIteration.StartupDelay > 0)
                {
                    await Task.Delay(TimeSpan.FromSeconds(httpIteration.StartupDelay), linkedToken);
                }

                // Final check before execution (in case termination just happened)
                if (iterationCts.IsCancellationRequested)
                {
                    return;
                }

                // Metrics collectors are self-managing via IIterationStatusMonitor
                // They start on registration and stop when iteration reaches terminal status
                await httpIterationCommand.ExecuteAsync(httpIteration, linkedToken);
            }
            catch (OperationCanceledException) when (iterationCts.IsCancellationRequested && !token.IsCancellationRequested)
            {
                // Terminated via termination rules - this is expected, log at verbose level
                await _logger.LogAsync(_runtimeOperationIdProvider.OperationId,
                    $"Scheduled execution of '{httpIteration.Name}' skipped - iteration was terminated", LPSLoggingLevel.Verbose);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                await _logger.LogAsync(_runtimeOperationIdProvider.OperationId,
                    $"Scheduled execution of '{httpIteration.Name}' has been cancelled", LPSLoggingLevel.Warning);
            }
        }

        /// <summary>
        /// Background task that monitors for iteration termination and cancels the CancellationTokenSource when detected.
        /// One watcher per iteration, shared by all clients.
        /// </summary>
        private async Task StartTerminationWatcherAsync(
            HttpIteration httpIteration,
            CancellationTokenSource iterationCts,
            CancellationToken externalToken)
        {
            try
            {
                while (!externalToken.IsCancellationRequested && !iterationCts.IsCancellationRequested)
                {
                    if (await _iterationStatusMonitor.IsTerminatedAsync(httpIteration, externalToken))
                    {
                        await _logger.LogAsync(_runtimeOperationIdProvider.OperationId,
                            $"Iteration '{httpIteration.Name}' terminated - cancelling all waiting clients",
                            LPSLoggingLevel.Information);

                        iterationCts.Cancel();
                        break;
                    }

                    // Poll every 500ms
                    await Task.Delay(500, externalToken);
                }
            }
            catch (OperationCanceledException)
            {
                // External cancellation - expected
            }
            catch (Exception ex)
            {
                await _logger.LogAsync(_runtimeOperationIdProvider.OperationId,
                    $"Termination watcher for '{httpIteration.Name}' failed: {ex.Message}",
                    LPSLoggingLevel.Error);
            }
        }
    }
}
