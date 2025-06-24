using LPS.Domain.Common.Interfaces;
using LPS.Domain.Domain.Common.Enums;
using LPS.Domain.Domain.Common.Interfaces;
using LPS.Domain.LPSRun.IterationMode;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace LPS.Domain
{
    public partial class HttpIteration
    {
        public class ExecuteCommand : IAsyncCommand<HttpIteration>
        {
            readonly IClientService<HttpRequest, HttpResponse> _httpClientService;
            public IClientService<HttpRequest, HttpResponse> HttpClientService => _httpClientService;
            readonly ILogger _logger;
            readonly IWatchdog _watchdog;
            readonly IRuntimeOperationIdProvider _runtimeOperationIdProvider;
            readonly IMetricsDataMonitor _lpsMonitoringEnroller;
            ITerminationCheckerService _terminationCheckerService;
            IIterationFailureEvaluator _iterationFailureEvaluator;
            readonly CancellationTokenSource _cts;

            protected ExecuteCommand()
            {
            }

            public ExecuteCommand(
                IClientService<HttpRequest, HttpResponse> httpClientService,
                ILogger logger,
                IWatchdog watchdog,
                IRuntimeOperationIdProvider runtimeOperationIdProvider,
                IMetricsDataMonitor lpsMonitoringEnroller,
                ITerminationCheckerService terminationCheckerService,
                IIterationFailureEvaluator iterationFailureEvaluator,
                CancellationTokenSource cts)
            {
                _httpClientService = httpClientService;
                _logger = logger;
                _watchdog = watchdog;
                _runtimeOperationIdProvider = runtimeOperationIdProvider;
                _lpsMonitoringEnroller = lpsMonitoringEnroller;
                _cts = cts;
                _terminationCheckerService = terminationCheckerService;
                _iterationFailureEvaluator = iterationFailureEvaluator;
                _executionStatus = ExecutionStatus.Scheduled;
            }
            private ExecutionStatus _executionStatus;
            public ExecutionStatus Status => _executionStatus;

            public async Task ExecuteAsync(HttpIteration entity)
            {
                if (entity == null)
                {
                    _logger.Log(_runtimeOperationIdProvider.OperationId, "LPSHttpIteration Entity Must Have a Value", LPSLoggingLevel.Error);
                    throw new ArgumentNullException(nameof(entity));
                }
                entity._logger = _logger;
                entity._watchdog = _watchdog;
                entity._runtimeOperationIdProvider = _runtimeOperationIdProvider;
                entity._lpsMonitoringEnroller = _lpsMonitoringEnroller;
                entity._terminationCheckerService = _terminationCheckerService;
                entity._cts = _cts;

                try
                {
                    _executionStatus = ExecutionStatus.Ongoing;
                    await entity.ExecuteAsync(this);
                }
                catch (OperationCanceledException) when (_cts.IsCancellationRequested)
                {
                    _executionStatus = ExecutionStatus.Cancelled;
                    throw;
                }  
                catch {  } // No exception should stop the iteration execution, termination rules and cancellations are the ways to stop ongoing execution
                finally {
                    if (_executionStatus != ExecutionStatus.Cancelled)
                    {
                        if (await _terminationCheckerService.IsTerminationRequiredAsync(entity))
                        {
                            _executionStatus = ExecutionStatus.Terminated;
                        }
                        else if (await _iterationFailureEvaluator.IsErrorRateExceededAsync(entity))
                        {
                            _executionStatus = ExecutionStatus.Failed;
                        }
                        else
                        {
                            _executionStatus = ExecutionStatus.Completed;
                        }
                    }
                }
            }

            public void CancellIfScheduled()
            {
                if (_executionStatus == ExecutionStatus.Scheduled)
                {
                    _executionStatus = ExecutionStatus.Cancelled;
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
            logEntry.AppendLine($"Http Method: {this.HttpRequest.HttpMethod.ToUpper()}");
            logEntry.AppendLine($"Http Version: {this.HttpRequest.HttpVersion}");
            logEntry.AppendLine($"URL: {this.HttpRequest.Url.Url}");

            if (!string.IsNullOrEmpty(this.HttpRequest.Payload?.RawValue) &&
                (this.HttpRequest.HttpMethod.Equals("PUT", StringComparison.OrdinalIgnoreCase) ||
                 this.HttpRequest.HttpMethod.Equals("POST", StringComparison.OrdinalIgnoreCase) ||
                 this.HttpRequest.HttpMethod.Equals("PATCH", StringComparison.OrdinalIgnoreCase)))
            {
                logEntry.AppendLine("...Begin Request Body...");
                logEntry.AppendLine(this.HttpRequest.Payload?.RawValue);
                logEntry.AppendLine("...End Request Body...");
            }
            else
            {
                logEntry.AppendLine("...Empty Payload...");
            }

            if (this.HttpRequest.HttpHeaders != null && this.HttpRequest.HttpHeaders.Count > 0)
            {
                logEntry.AppendLine("...Begin Request Headers...");
                foreach (var header in this.HttpRequest.HttpHeaders)
                {
                    logEntry.AppendLine($"{header.Key}: {header.Value}");
                }
                logEntry.AppendLine("...End Request Headers...");
            }
            else
            {
                logEntry.AppendLine("...No Headers Were Provided...");
            }

            await _logger.LogAsync(_runtimeOperationIdProvider.OperationId, logEntry.ToString(), LPSLoggingLevel.Verbose, _cts.Token);
        }

        private int _numberOfSentRequests = 0;

        private async Task ExecuteAsync(ExecuteCommand command)
        {
            if (!this.IsValid)
                return;

            var httpRequestExecuteCommand = new HttpRequest.ExecuteCommand(command.HttpClientService, _logger, _watchdog, _runtimeOperationIdProvider, _cts);
            try
            {
                await LogRequestDetails();

                IIterationModeService iterationModeService;
                IBatchProcessor<HttpRequest.ExecuteCommand, HttpRequest> batchProcessor;

                switch (this.Mode)
                {
                    case IterationMode.DCB:
                        batchProcessor = new BatchProcessor(this, HttpRequest, _watchdog);
                        iterationModeService = new DCBMode(
                            httpRequestExecuteCommand,
                            duration: this.Duration.Value,
                            coolDownTime: this.CoolDownTime.Value,
                            batchSize: this.BatchSize.Value,
                            maximizeThroughput: this.MaximizeThroughput,
                            batchProcessor: batchProcessor,
                            this, _terminationCheckerService
                        );
                        break;

                    case IterationMode.CRB:
                        batchProcessor = new BatchProcessor(this, HttpRequest, _watchdog);
                        iterationModeService = new CRBMode(
                            httpRequestExecuteCommand,
                            requestCount: this.RequestCount.Value,
                            coolDownTime: this.CoolDownTime.Value,
                            batchSize: this.BatchSize.Value,
                            maximizeThroughput: this.MaximizeThroughput,
                            batchProcessor: batchProcessor,
                            this, _terminationCheckerService
                        );
                        break;

                    case IterationMode.CB:
                        batchProcessor = new BatchProcessor(this, HttpRequest, _watchdog);
                        iterationModeService = new CBMode(
                            httpRequestExecuteCommand,
                            coolDownTime: this.CoolDownTime.Value,
                            batchSize: this.BatchSize.Value,
                            maximizeThroughput: this.MaximizeThroughput,
                            batchProcessor: batchProcessor,
                            this,_terminationCheckerService
                        );
                        break;

                    case IterationMode.R:
                        iterationModeService = new RMode(
                            httpIteration: this,
                            request: this.HttpRequest,
                            command: httpRequestExecuteCommand,
                            requestCount: this.RequestCount.Value,
                            watchdog: _watchdog, _terminationCheckerService
                        );
                        break;

                    case IterationMode.D:
                        iterationModeService = new DMode(
                            httpIteration: this,
                            request: this.HttpRequest,
                            command: httpRequestExecuteCommand,
                            duration: this.Duration.Value,
                            watchdog: _watchdog, _terminationCheckerService
                        );
                        break;

                    default:
                        throw new ArgumentException("Invalid iteration mode was chosen");
                }

                _numberOfSentRequests = await iterationModeService.ExecuteAsync(_cts.Token);

                await _logger.LogAsync(_runtimeOperationIdProvider.OperationId, $"The client {command.HttpClientService.SessionId} has sent {_numberOfSentRequests} request(s) to {this.HttpRequest.Url.Url}", LPSLoggingLevel.Verbose, _cts.Token);
                await _logger.LogAsync(_runtimeOperationIdProvider.OperationId, $"The client {command.HttpClientService.SessionId} is waiting for the {_numberOfSentRequests} request(s) to complete", LPSLoggingLevel.Verbose, _cts.Token);
            }
            catch (OperationCanceledException) when (_cts.IsCancellationRequested)
            {
                throw;
            }
        }
    }
}
