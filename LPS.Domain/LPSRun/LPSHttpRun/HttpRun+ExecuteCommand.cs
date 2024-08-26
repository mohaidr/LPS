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
            private readonly ProtectedAccessTestPlanExecuteCommand protectedAccessTestPlanExecuteCommand = new ProtectedAccessTestPlanExecuteCommand();
            private class ProtectedAccessTestPlanExecuteCommand : TestPlan.ExecuteCommand
            {
                public new int SafelyIncrementNumberofSentRequests(TestPlan.ExecuteCommand command)
                {
                    return base.SafelyIncrementNumberofSentRequests(command);
                }
            }
            public TestPlan.ExecuteCommand LPSTestPlanExecuteCommand { get; set; }

            IClientService<HttpRequestProfile, HttpResponse> _httpClientService;
            ILogger _logger;
            IWatchdog _watchdog;
            IRuntimeOperationIdProvider _runtimeOperationIdProvider;
            IMetricsDataMonitor _lpsMonitoringEnroller;
            ICommandStatusMonitor<IAsyncCommand<HttpRun>, HttpRun> _httpRunExecutionCommandStatusMonitor;
            CancellationTokenSource _cts;
            protected ExecuteCommand()
            {

            }
            public ExecuteCommand(IClientService<HttpRequestProfile,
                HttpResponse> httpClientService,
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
            async public Task ExecuteAsync(HttpRun entity)
            {
                if (entity == null)
                {
                    _logger.Log(_runtimeOperationIdProvider.OperationId, "LPSHttpRun Entity Must Have a Value", LPSLoggingLevel.Error);
                    throw new ArgumentNullException(nameof(entity));
                }
                entity._httpClientService = this._httpClientService;
                entity._logger = this._logger;
                entity._watchdog = this._watchdog;
                entity._runtimeOperationIdProvider = this._runtimeOperationIdProvider;
                entity._lpsMonitoringEnroller = this._lpsMonitoringEnroller;
                entity._cts = this._cts;
                try
                {
                    _executionStatus = AsyncCommandStatus.Ongoing;
                    await entity.ExecuteAsync(this);
                    _executionStatus =  _numberOfFailedToCompleteRequests>0 ? AsyncCommandStatus.Failed : _cts.Token.IsCancellationRequested ? AsyncCommandStatus.Cancelled : AsyncCommandStatus.Completed;

                }
                catch
                {
                    _executionStatus = AsyncCommandStatus.Failed;
                    throw;
                }
            }

            private int _numberOfSuccessfullyCompletedRequests;
            private int _numberOfFailedToCompleteRequests;
            private int _numberOfSentRequests;
            public int NumberOfSentRequests { get { return _numberOfSentRequests; } }
            public int NumberOfSuccessfullyCompletedRequests { get { return _numberOfSuccessfullyCompletedRequests; } }
            public int NumberOfFailedToCompleteRequests { get { return _numberOfFailedToCompleteRequests; } }
            private AsyncCommandStatus _executionStatus;
            public AsyncCommandStatus Status => _executionStatus;

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
                protectedAccessTestPlanExecuteCommand.SafelyIncrementNumberofSentRequests(execCommand.LPSTestPlanExecuteCommand);
                return Interlocked.Increment(ref execCommand._numberOfSentRequests);
            }
        }
        async private Task ExecuteAsync(ExecuteCommand command)
        {
            if (this.IsValid)
            {

                #region Logging Request Details
                string logEntry = string.Empty;

                logEntry += "Run Details\n";
                logEntry += $"Id: {this.Id}\n";
                logEntry += $"Iteration Mode: {this.Mode}\n";
                logEntry += $"Request Count: {this.RequestCount}\n";
                logEntry += $"Duration: {this.Duration}\n";
                logEntry += $"Batch Size: {this.BatchSize}\n";
                logEntry += $"Cool Down Time: {this.CoolDownTime}\n";
                logEntry += $"Http Method: {this.LPSHttpRequestProfile.HttpMethod.ToUpper()}\n";
                logEntry += $"Http Version: {this.LPSHttpRequestProfile.Httpversion}\n";
                logEntry += $"URL: {this.LPSHttpRequestProfile.URL}\n";

                if (!string.IsNullOrEmpty(this.LPSHttpRequestProfile.Payload)
                    && (this.LPSHttpRequestProfile.HttpMethod.ToUpper() == "PUT"
                    || this.LPSHttpRequestProfile.HttpMethod.ToUpper() == "POST"
                    || this.LPSHttpRequestProfile.HttpMethod.ToUpper() == "PATCH"))
                {
                    logEntry += "...Begin Request Body...\n";
                    logEntry += this.LPSHttpRequestProfile.Payload + "\n";
                    logEntry += "...End Request Body...\n";
                }
                else
                {
                    logEntry += "...Empty Payload...\n";
                }

                if (this.LPSHttpRequestProfile.HttpHeaders != null && this.LPSHttpRequestProfile.HttpHeaders.Count > 0)
                {
                    logEntry += "...Begin Request Headers...\n";

                    foreach (var header in this.LPSHttpRequestProfile.HttpHeaders)
                    {
                        logEntry += $"{header.Key}: {header.Value}\n";
                    }

                    logEntry += "...End Request Headers...\n";
                }
                else
                {
                    logEntry += "...No Headers Were Provided...\n";
                }

                await _logger.LogAsync(_runtimeOperationIdProvider.OperationId, logEntry, LPSLoggingLevel.Verbose, _cts);
                #endregion

                HttpRequestProfile.ExecuteCommand lpsRequestProfileExecCommand = new HttpRequestProfile.ExecuteCommand(this._httpClientService, command, _logger, _watchdog, _runtimeOperationIdProvider, _cts) ;
                TaskCompletionSource<bool> taskCompletionSource = new TaskCompletionSource<bool>();
                Stopwatch stopwatch;
                int numberOfSentRequests = 0;
                string hostName = new Uri(this.LPSHttpRequestProfile.URL).Host;
                switch (this.Mode)
                {
                    case IterationMode.DCB:
                        stopwatch = new Stopwatch();
                        stopwatch.Start();
                        while (stopwatch.Elapsed.TotalSeconds < this.Duration.Value && !_cts.Token.IsCancellationRequested)
                        {
                            for (int b = 0; b < this.BatchSize && stopwatch.Elapsed.TotalSeconds < this.Duration.Value; b++)
                            {
                                await _watchdog.BalanceAsync(hostName);
                                _ = lpsRequestProfileExecCommand.ExecuteAsync(LPSHttpRequestProfile);
                                numberOfSentRequests++;
                            }
                            await Task.Delay((int)TimeSpan.FromSeconds(this.CoolDownTime.Value).TotalMilliseconds, _cts.Token);
                        }
                        stopwatch.Stop();
                        break;
                    case IterationMode.CRB:
                        for (int i = 0; i < this.RequestCount.Value && !_cts.Token.IsCancellationRequested; i += this.BatchSize.Value)
                        {
                            for (int b = 0; b < this.BatchSize && numberOfSentRequests < this.RequestCount.Value; b++)
                            {
                                await _watchdog.BalanceAsync(hostName);
                                _= lpsRequestProfileExecCommand.ExecuteAsync(LPSHttpRequestProfile);
                                numberOfSentRequests++;
                            }
                            await Task.Delay((int)TimeSpan.FromSeconds(this.CoolDownTime.Value).TotalMilliseconds, _cts.Token);
                        }
                        break;
                    case IterationMode.CB:
                        while (!_cts.Token.IsCancellationRequested)
                        {
                            for (int b = 0; b < this.BatchSize; b++)
                            {
                                await _watchdog.BalanceAsync(hostName);
                                _ = lpsRequestProfileExecCommand.ExecuteAsync(LPSHttpRequestProfile);
                                numberOfSentRequests++;
                            }
                            await Task.Delay((int)TimeSpan.FromSeconds(this.CoolDownTime.Value).TotalMilliseconds, _cts.Token);
                            
                        }
                        break;
                    case IterationMode.R:
                        for (int i = 0; i < this.RequestCount && !_cts.Token.IsCancellationRequested; i++)
                        {
                            await _watchdog.BalanceAsync(hostName);
                            await lpsRequestProfileExecCommand.ExecuteAsync(LPSHttpRequestProfile);
                            numberOfSentRequests++;
                        }
                        break;
                    case IterationMode.D:
                        stopwatch = new Stopwatch();
                        stopwatch.Start();
                        while (stopwatch.Elapsed.TotalSeconds < this.Duration.Value && !_cts.Token.IsCancellationRequested)
                        {
                            await _watchdog.BalanceAsync(hostName);
                            await lpsRequestProfileExecCommand.ExecuteAsync(LPSHttpRequestProfile);
                            numberOfSentRequests++;
                        }
                        stopwatch.Stop();
                        break;
                    default:
                        throw new ArgumentException("Invalid iteration mode was chosen");
                }

                await _logger.LogAsync(_runtimeOperationIdProvider.OperationId, $"The client {_httpClientService.Id} has sent {numberOfSentRequests} request(s) to {this.LPSHttpRequestProfile.URL}", LPSLoggingLevel.Verbose, _cts);
                await _logger.LogAsync(_runtimeOperationIdProvider.OperationId, $"The client {_httpClientService.Id} is waiting for the {numberOfSentRequests} request(s) to complete", LPSLoggingLevel.Verbose, _cts);

                //TODO: Change this logic to event driven to avoid unnecessary conext switching every 1 second
                //Also the approach of knowing if the test has completed by counters may not be the best so look for some other solution

                while (command.NumberOfSuccessfullyCompletedRequests + command.NumberOfFailedToCompleteRequests < numberOfSentRequests)
                {
                    await Task.Delay(1000);
                }

                taskCompletionSource.SetResult(true);
                await _logger.LogAsync(_runtimeOperationIdProvider.OperationId, $"The client {_httpClientService.Id} has completed all the requests to {this.LPSHttpRequestProfile.URL} with {command.NumberOfSuccessfullyCompletedRequests} successfully completed requests and {command.NumberOfFailedToCompleteRequests} failed to complete requests", LPSLoggingLevel.Verbose, _cts);
            }
        }
    }
}
