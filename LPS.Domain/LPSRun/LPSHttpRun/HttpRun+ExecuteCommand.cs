using LPS.Domain.Common.Interfaces;
using LPS.Domain.Domain.Common.Interfaces;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace LPS.Domain
{
    public partial class HttpRun
    {
        public class ExecuteCommand : IAsyncCommand<HttpRun>
        {
            public TestPlan.ExecuteCommand LPSTestPlanExecuteCommand { get; set; }

            private readonly ProtectedAccessTestPlanExecuteCommand _protectedAccessTestPlanExecuteCommand = new ProtectedAccessTestPlanExecuteCommand();

            private IClientService<HttpRequestProfile, HttpResponse> _httpClientService;
            private ILogger _logger;
            private IWatchdog _watchdog;
            private IRuntimeOperationIdProvider _runtimeOperationIdProvider;
            private IMetricsDataMonitor _lpsMonitoringEnroller;
            private ICommandStatusMonitor<IAsyncCommand<HttpRun>, HttpRun> _httpRunExecutionCommandStatusMonitor;
            private CancellationTokenSource _cts;

            private int _numberOfSuccessfullyCompletedRequests;
            private int _numberOfFailedToCompleteRequests;
            private int _numberOfSentRequests;

            private AsyncCommandStatus _executionStatus;

            protected ExecuteCommand()
            {
            }

            public ExecuteCommand(
                IClientService<HttpRequestProfile, HttpResponse> httpClientService,
                TestPlan.ExecuteCommand planExecCommand,
                ILogger logger,
                IWatchdog watchdog,
                IRuntimeOperationIdProvider runtimeOperationIdProvider,
                IMetricsDataMonitor lpsMonitoringEnroller,
                ICommandStatusMonitor<IAsyncCommand<HttpRun>, HttpRun> httpRunExecutionCommandStatusMonitor,
                CancellationTokenSource cts)
            {
                _httpClientService = httpClientService;
                LPSTestPlanExecuteCommand = planExecCommand;
                _logger = logger;
                _watchdog = watchdog;
                _runtimeOperationIdProvider = runtimeOperationIdProvider;
                _lpsMonitoringEnroller = lpsMonitoringEnroller;
                _httpRunExecutionCommandStatusMonitor = httpRunExecutionCommandStatusMonitor;
                _cts = cts;
            }

            public int NumberOfSentRequests => _numberOfSentRequests;
            public int NumberOfSuccessfullyCompletedRequests => _numberOfSuccessfullyCompletedRequests;
            public int NumberOfFailedToCompleteRequests => _numberOfFailedToCompleteRequests;
            public AsyncCommandStatus Status => _executionStatus;

            public async Task ExecuteAsync(HttpRun entity)
            {
                if (entity == null)
                {
                    _logger.Log(_runtimeOperationIdProvider.OperationId, "LPSHttpRun Entity Must Have a Value", LPSLoggingLevel.Error);
                    throw new ArgumentNullException(nameof(entity));
                }

                entity._httpClientService = _httpClientService;
                entity._logger = _logger;
                entity._watchdog = _watchdog;
                entity._runtimeOperationIdProvider = _runtimeOperationIdProvider;
                entity._lpsMonitoringEnroller = _lpsMonitoringEnroller;
                entity._cts = _cts;

                try
                {
                    _executionStatus = AsyncCommandStatus.Ongoing;
                    await entity.ExecuteAsync(this);
                    _executionStatus = _numberOfFailedToCompleteRequests > 0
                        ? AsyncCommandStatus.Failed
                        : _cts.Token.IsCancellationRequested
                            ? AsyncCommandStatus.Cancelled
                            : AsyncCommandStatus.Completed;
                }
                catch
                {
                    _executionStatus = AsyncCommandStatus.Failed;
                    throw;
                }
            }

            protected int SafelyIncrementNumberOfSuccessfulRequests(ExecuteCommand execCommand)
            {
                return Interlocked.Increment(ref execCommand._numberOfSuccessfullyCompletedRequests);
            }

            protected int SafelyIncrementNumberOfFailedRequests(ExecuteCommand execCommand)
            {
                return Interlocked.Increment(ref execCommand._numberOfFailedToCompleteRequests);
            }

            protected int SafelyIncrementNumberofSentRequests(ExecuteCommand execCommand)
            {
                _protectedAccessTestPlanExecuteCommand.SafelyIncrementNumberofSentRequests(execCommand.LPSTestPlanExecuteCommand);
                return Interlocked.Increment(ref execCommand._numberOfSentRequests);
            }

            private class ProtectedAccessTestPlanExecuteCommand : TestPlan.ExecuteCommand
            {
                public new int SafelyIncrementNumberofSentRequests(TestPlan.ExecuteCommand command)
                {
                    return base.SafelyIncrementNumberofSentRequests(command);
                }
            }
        }

        private async Task LogRequestDetails()
        {
            var logEntry = new System.Text.StringBuilder();

            logEntry.AppendLine("Run Details");
            logEntry.AppendLine($"Id: {this.Id}");
            logEntry.AppendLine($"Iteration Mode: {this.Mode}");
            logEntry.AppendLine($"Request Count: {this.RequestCount}");
            logEntry.AppendLine($"Duration: {this.Duration}");
            logEntry.AppendLine($"Batch Size: {this.BatchSize}");
            logEntry.AppendLine($"Cool Down Time: {this.CoolDownTime}");
            logEntry.AppendLine($"Http Method: {this.LPSHttpRequestProfile.HttpMethod.ToUpper()}");
            logEntry.AppendLine($"Http Version: {this.LPSHttpRequestProfile.Httpversion}");
            logEntry.AppendLine($"URL: {this.LPSHttpRequestProfile.URL}");

            if (!string.IsNullOrEmpty(this.LPSHttpRequestProfile.Payload) &&
                (this.LPSHttpRequestProfile.HttpMethod.Equals("PUT", StringComparison.OrdinalIgnoreCase) ||
                 this.LPSHttpRequestProfile.HttpMethod.Equals("POST", StringComparison.OrdinalIgnoreCase) ||
                 this.LPSHttpRequestProfile.HttpMethod.Equals("PATCH", StringComparison.OrdinalIgnoreCase)))
            {
                logEntry.AppendLine("...Begin Request Body...");
                logEntry.AppendLine(this.LPSHttpRequestProfile.Payload);
                logEntry.AppendLine("...End Request Body...");
            }
            else
            {
                logEntry.AppendLine("...Empty Payload...");
            }

            if (this.LPSHttpRequestProfile.HttpHeaders != null && this.LPSHttpRequestProfile.HttpHeaders.Count > 0)
            {
                logEntry.AppendLine("...Begin Request Headers...");
                foreach (var header in this.LPSHttpRequestProfile.HttpHeaders)
                {
                    logEntry.AppendLine($"{header.Key}: {header.Value}");
                }
                logEntry.AppendLine("...End Request Headers...");
            }
            else
            {
                logEntry.AppendLine("...No Headers Were Provided...");
            }

            await _logger.LogAsync(_runtimeOperationIdProvider.OperationId, logEntry.ToString(), LPSLoggingLevel.Verbose, _cts);
        }

        private int _numberOfSentRequests = 0;

        private async Task ExecuteAsync(ExecuteCommand command)
        {
            if (!this.IsValid)
            {
                return;
            }

            await LogRequestDetails();

            var lpsRequestProfileExecCommand = new HttpRequestProfile.ExecuteCommand(_httpClientService, command, _logger, _watchdog, _runtimeOperationIdProvider, _cts);
            var taskCompletionSource = new TaskCompletionSource<bool>();
            var hostName = new Uri(this.LPSHttpRequestProfile.URL).Host;

            switch (this.Mode)
            {
                case IterationMode.DCB:
                    await ExecuteDCBModeAsync(lpsRequestProfileExecCommand, hostName);
                    break;
                case IterationMode.CRB:
                    await ExecuteCRBModeAsync(lpsRequestProfileExecCommand, hostName);
                    break;
                case IterationMode.CB:
                    await ExecuteCBModeAsync(lpsRequestProfileExecCommand, hostName);
                    break;
                case IterationMode.R:
                    await ExecuteRModeAsync(lpsRequestProfileExecCommand, hostName);
                    break;
                case IterationMode.D:
                    await ExecuteDModeAsync(lpsRequestProfileExecCommand, hostName);
                    break;
                default:
                    throw new ArgumentException("Invalid iteration mode was chosen");
            }

            await _logger.LogAsync(_runtimeOperationIdProvider.OperationId, $"The client {_httpClientService.Id} has sent {_numberOfSentRequests} request(s) to {this.LPSHttpRequestProfile.URL}", LPSLoggingLevel.Verbose, _cts);
            await _logger.LogAsync(_runtimeOperationIdProvider.OperationId, $"The client {_httpClientService.Id} is waiting for the {_numberOfSentRequests} request(s) to complete", LPSLoggingLevel.Verbose, _cts);

            while (command.NumberOfSuccessfullyCompletedRequests + command.NumberOfFailedToCompleteRequests < _numberOfSentRequests)
            {
                await Task.Delay(1000);
            }

            taskCompletionSource.SetResult(true);
            await _logger.LogAsync(_runtimeOperationIdProvider.OperationId, $"The client {_httpClientService.Id} has completed all the requests to {this.LPSHttpRequestProfile.URL} with {command.NumberOfSuccessfullyCompletedRequests} successfully completed requests and {command.NumberOfFailedToCompleteRequests} failed to complete requests", LPSLoggingLevel.Verbose, _cts);
        }

        private async Task ExecuteDCBModeAsync(HttpRequestProfile.ExecuteCommand lpsRequestProfileExecCommand, string hostName)
        {
            var stopwatch = Stopwatch.StartNew();
            var coolDownWatch = Stopwatch.StartNew();

            while (stopwatch.Elapsed.TotalSeconds < this.Duration.Value && !_cts.Token.IsCancellationRequested)
            {
                if (MaximizeThroughput)
                {
                    bool newBatch = coolDownWatch.Elapsed.TotalMilliseconds >= CoolDownTime;
                    if (newBatch)
                    {
                        coolDownWatch.Restart();
                        await Task.Yield();
                        for (int b = 0; b < this.BatchSize && stopwatch.Elapsed.TotalSeconds < this.Duration.Value; b++)
                        {
                            await _watchdog.BalanceAsync(hostName);
                            _ = lpsRequestProfileExecCommand.ExecuteAsync(LPSHttpRequestProfile);
                            _numberOfSentRequests++;
                        }
                    }
                }
                else
                {
                    coolDownWatch.Restart();
                    for (int b = 0; b < this.BatchSize && stopwatch.Elapsed.TotalSeconds < this.Duration.Value; b++)
                    {
                        await _watchdog.BalanceAsync(hostName);
                        _ = lpsRequestProfileExecCommand.ExecuteAsync(LPSHttpRequestProfile);
                        _numberOfSentRequests++;
                    }
                    await Task.Delay((int)Math.Max(CoolDownTime.Value, CoolDownTime.Value - coolDownWatch.Elapsed.TotalMilliseconds));
                }
            }

            coolDownWatch.Stop();
            stopwatch.Stop();
        }

        private async Task ExecuteCRBModeAsync(HttpRequestProfile.ExecuteCommand lpsRequestProfileExecCommand, string hostName)
        {
            var coolDownWatch = Stopwatch.StartNew();

            while (_numberOfSentRequests < this.RequestCount.Value && !_cts.Token.IsCancellationRequested)
            {
                if (MaximizeThroughput)
                {
                    bool newBatch = coolDownWatch.Elapsed.TotalMilliseconds >= CoolDownTime;
                    if (newBatch)
                    {
                        coolDownWatch.Restart();
                        await Task.Yield();
                        for (int b = 0; b < this.BatchSize && _numberOfSentRequests < this.RequestCount.Value; b++)
                        {
                            await _watchdog.BalanceAsync(hostName);
                            _ = lpsRequestProfileExecCommand.ExecuteAsync(LPSHttpRequestProfile);
                            _numberOfSentRequests++;
                        }
                    }
                }
                else
                {
                    coolDownWatch.Restart();
                    for (int b = 0; b < this.BatchSize && _numberOfSentRequests < this.RequestCount.Value; b++)
                    {
                        await _watchdog.BalanceAsync(hostName);
                        _ = lpsRequestProfileExecCommand.ExecuteAsync(LPSHttpRequestProfile);
                        _numberOfSentRequests++;
                    }
                    await Task.Delay((int)Math.Max(CoolDownTime.Value, CoolDownTime.Value - coolDownWatch.Elapsed.TotalMilliseconds));
                }
            }

            coolDownWatch.Stop();
        }

        private async Task ExecuteCBModeAsync(HttpRequestProfile.ExecuteCommand lpsRequestProfileExecCommand, string hostName)
        {
            var coolDownWatch = Stopwatch.StartNew();

            while (!_cts.Token.IsCancellationRequested)
            {
                if (MaximizeThroughput)
                {
                    bool newBatch = coolDownWatch.Elapsed.TotalMilliseconds >= CoolDownTime;
                    if (newBatch)
                    {
                        coolDownWatch.Restart();
                        await Task.Yield();
                        for (int b = 0; b < this.BatchSize; b++)
                        {
                            await _watchdog.BalanceAsync(hostName);
                            _ = lpsRequestProfileExecCommand.ExecuteAsync(LPSHttpRequestProfile);
                            _numberOfSentRequests++;
                        }
                    }
                }
                else
                {
                    coolDownWatch.Restart();
                    for (int b = 0; b < this.BatchSize; b++)
                    {
                        await _watchdog.BalanceAsync(hostName);
                        _ = lpsRequestProfileExecCommand.ExecuteAsync(LPSHttpRequestProfile);
                        _numberOfSentRequests++;
                    }
                    await Task.Delay((int)Math.Max(CoolDownTime.Value, CoolDownTime.Value - coolDownWatch.Elapsed.TotalMilliseconds));
                }
            }

            coolDownWatch.Stop();
        }

        private async Task ExecuteRModeAsync(HttpRequestProfile.ExecuteCommand lpsRequestProfileExecCommand, string hostName)
        {
            for (int i = 0; i < this.RequestCount && !_cts.Token.IsCancellationRequested; i++)
            {
                await _watchdog.BalanceAsync(hostName);
                await lpsRequestProfileExecCommand.ExecuteAsync(LPSHttpRequestProfile);
                _numberOfSentRequests++;
            }
        }

        private async Task ExecuteDModeAsync(HttpRequestProfile.ExecuteCommand lpsRequestProfileExecCommand, string hostName)
        {
            var stopwatch = Stopwatch.StartNew();
            while (stopwatch.Elapsed.TotalSeconds < this.Duration.Value && !_cts.Token.IsCancellationRequested)
            {
                await _watchdog.BalanceAsync(hostName);
                await lpsRequestProfileExecCommand.ExecuteAsync(LPSHttpRequestProfile);
                _numberOfSentRequests++;
            }
            stopwatch.Stop();
        }
        private async Task SendBatchAsync(HttpRequestProfile.ExecuteCommand lpsRequestProfileExecCommand, string hostName, int batchSize, Func<bool> batchCondition)
        {
            for (int b = 0; b < batchSize && batchCondition(); b++)
            {
                await _watchdog.BalanceAsync(hostName);
                _ = lpsRequestProfileExecCommand.ExecuteAsync(LPSHttpRequestProfile);
                _numberOfSentRequests++;
            }
        }
    }
}
