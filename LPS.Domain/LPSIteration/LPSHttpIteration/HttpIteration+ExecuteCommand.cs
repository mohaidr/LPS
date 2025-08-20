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
            IIterationStatusMonitor _iterationStatusMonitor;
            protected ExecuteCommand()
            {
            }
            
            public ExecuteCommand(
                IClientService<HttpRequest, HttpResponse> httpClientService,
                ILogger logger,
                IWatchdog watchdog,
                IRuntimeOperationIdProvider runtimeOperationIdProvider,
                IMetricsDataMonitor lpsMonitoringEnroller,
                IIterationStatusMonitor iterationStatusMonitor)
            {
                _httpClientService = httpClientService;
                _logger = logger;
                _watchdog = watchdog;
                _runtimeOperationIdProvider = runtimeOperationIdProvider;
                _lpsMonitoringEnroller = lpsMonitoringEnroller;
                _iterationStatusMonitor = iterationStatusMonitor;
                _executionStatus = CommandExecutionStatus.Scheduled;
            }
            private CommandExecutionStatus _executionStatus;
            public CommandExecutionStatus Status => _executionStatus;

            public async Task ExecuteAsync(HttpIteration entity, CancellationToken token)
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
                entity._iterationStatusMonitor = _iterationStatusMonitor;
                try
                {
                    if (await entity._skipIfEvaluator.ShouldSkipAsync(entity.SkipIf, _httpClientService.SessionId, token))
                    {
                        await _logger.LogAsync(_runtimeOperationIdProvider.OperationId, $"Iteration {entity.Name} is being skipped because the condition '{entity.SkipIf}' evaluated to true.", LPSLoggingLevel.Information, token);
                        _executionStatus = CommandExecutionStatus.Skipped;
                        return;
                    }
                    _executionStatus = CommandExecutionStatus.Ongoing;
                    await entity.ExecuteAsync(this, token);
                    _executionStatus = CommandExecutionStatus.Completed;
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested)
                {
                    _executionStatus = CommandExecutionStatus.Cancelled;
                    throw;
                }
                catch
                {
                    _executionStatus = CommandExecutionStatus.Failed;
                } // No exception should stop the iteration execution, termination rules and cancellations are the ways to stop ongoing execution
                finally
                {
                    if (await _iterationStatusMonitor.IsTerminatedAsync(entity, token))
                        _executionStatus = CommandExecutionStatus.Terminated;
                }
            }
        }

        private async Task LogRequestDetailsAsync(CancellationToken token)
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

            await _logger.LogAsync(_runtimeOperationIdProvider.OperationId, logEntry.ToString(), LPSLoggingLevel.Verbose, token);
        }

        private async Task ExecuteAsync(ExecuteCommand command, CancellationToken token)
        {
            if (!this.IsValid)
                return;

            var httpRequestExecuteCommand = new HttpRequest.ExecuteCommand(command.HttpClientService, _logger, _watchdog, _runtimeOperationIdProvider);
            try
            {
                if (await _skipIfEvaluator.ShouldSkipAsync(SkipIf, command.HttpClientService.SessionId, token))
                {
                    await _logger.LogAsync(_runtimeOperationIdProvider.OperationId, $"Iteration {Name} is being skipped because the condition '{SkipIf}' evaluated to true.", LPSLoggingLevel.Information, token);
                    return;
                }
                await LogRequestDetailsAsync(token);

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
                            this, _iterationStatusMonitor
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
                            this, _iterationStatusMonitor
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
                            this, _iterationStatusMonitor
                        );
                        break;

                    case IterationMode.R:
                        iterationModeService = new RMode(
                            httpIteration: this,
                            request: this.HttpRequest,
                            command: httpRequestExecuteCommand,
                            requestCount: this.RequestCount.Value,
                            watchdog: _watchdog, _iterationStatusMonitor
                        );
                        break;

                    case IterationMode.D:
                        iterationModeService = new DMode(
                            httpIteration: this,
                            request: this.HttpRequest,
                            command: httpRequestExecuteCommand,
                            duration: this.Duration.Value,
                            watchdog: _watchdog, _iterationStatusMonitor
                        );
                        break;

                    default:
                        throw new ArgumentException("Invalid iteration mode was chosen");
                }

                int _numberOfSentRequests = await iterationModeService.ExecuteAsync(token);

                await _logger.LogAsync(_runtimeOperationIdProvider.OperationId, $"The client {command.HttpClientService.SessionId} has sent {_numberOfSentRequests} request(s) to {this.HttpRequest.Url.Url}", LPSLoggingLevel.Verbose, token);
                await _logger.LogAsync(_runtimeOperationIdProvider.OperationId, $"The client {command.HttpClientService.SessionId} is waiting for the {_numberOfSentRequests} request(s) to complete", LPSLoggingLevel.Verbose, token);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                throw;
            }
        }
    }
}
