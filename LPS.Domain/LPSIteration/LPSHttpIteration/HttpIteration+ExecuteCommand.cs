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
        public class ExecuteCommand : IAsyncCommand<HttpIteration>, IStateObserver
        {
            readonly IClientService<HttpRequestProfile, HttpResponse> _httpClientService;
            readonly ILogger _logger;
            readonly IWatchdog _watchdog;
            readonly IRuntimeOperationIdProvider _runtimeOperationIdProvider;
            readonly IMetricsDataMonitor _lpsMonitoringEnroller;
            readonly CancellationTokenSource _cts;


            protected ExecuteCommand()
            {
            }

            public ExecuteCommand(
                IClientService<HttpRequestProfile, HttpResponse> httpClientService,
                ILogger logger,
                IWatchdog watchdog,
                IRuntimeOperationIdProvider runtimeOperationIdProvider,
                IMetricsDataMonitor lpsMonitoringEnroller,
                CancellationTokenSource cts)
            {
                _httpClientService = httpClientService;
                _logger = logger;
                _watchdog = watchdog;
                _runtimeOperationIdProvider = runtimeOperationIdProvider;
                _lpsMonitoringEnroller = lpsMonitoringEnroller;
                _cts = cts;
                _executionStatus = ExecutionStatus.Scheduled;
            }
            private ExecutionStatus _executionStatus;
            private ExecutionStatus _finalStatus;
            public ExecutionStatus Status => _executionStatus;

            public async Task ExecuteAsync(HttpIteration entity)
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
                    _executionStatus = ExecutionStatus.Ongoing;
                    await entity.ExecuteAsync(this);
                    _executionStatus = _finalStatus;
                }
                catch (OperationCanceledException) when (_cts.IsCancellationRequested)
                {
                    _executionStatus = ExecutionStatus.Cancelled;
                    throw;
                }
                catch
                {
                    _executionStatus = ExecutionStatus.Failed;
                }
                finally {
                    if (_finalStatus > _executionStatus)
                        _executionStatus = _finalStatus;
                }
            }
            public void NotifyMe(ExecutionStatus status)
            {
                _finalStatus = status;
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
            logEntry.AppendLine($"Http Method: {this.RequestProfile.HttpMethod.ToUpper()}");
            logEntry.AppendLine($"Http Version: {this.RequestProfile.HttpVersion}");
            logEntry.AppendLine($"URL: {this.RequestProfile.URL}");

            if (!string.IsNullOrEmpty(this.RequestProfile.Payload) &&
                (this.RequestProfile.HttpMethod.Equals("PUT", StringComparison.OrdinalIgnoreCase) ||
                 this.RequestProfile.HttpMethod.Equals("POST", StringComparison.OrdinalIgnoreCase) ||
                 this.RequestProfile.HttpMethod.Equals("PATCH", StringComparison.OrdinalIgnoreCase)))
            {
                logEntry.AppendLine("...Begin Request Body...");
                logEntry.AppendLine(this.RequestProfile.Payload);
                logEntry.AppendLine("...End Request Body...");
            }
            else
            {
                logEntry.AppendLine("...Empty Payload...");
            }

            if (this.RequestProfile.HttpHeaders != null && this.RequestProfile.HttpHeaders.Count > 0)
            {
                logEntry.AppendLine("...Begin Request Headers...");
                foreach (var header in this.RequestProfile.HttpHeaders)
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
            {
                return;
            }
            var profileCommand = new HttpRequestProfile.ExecuteCommand(_httpClientService, _logger, _watchdog, _runtimeOperationIdProvider, _cts);
            profileCommand.RegisterObserver(command);
            try
            {

                await LogRequestDetails();

                IIterationModeService iterationModeService;
                // Create a batch processor if needed
                IBatchProcessor<HttpRequestProfile.ExecuteCommand, HttpRequestProfile> batchProcessor;
                switch (this.Mode)
                {
                    case IterationMode.DCB:
                        batchProcessor = new BatchProcessor(RequestProfile, _watchdog);
                        iterationModeService = new DCBMode.Builder()
                            .SetCommand(profileCommand)
                            .SetDuration(this.Duration.Value)
                            .SetCoolDownTime(this.CoolDownTime.Value)
                            .SetBatchSize(this.BatchSize.Value)
                            .SetMaximizeThroughput(this.MaximizeThroughput)
                            .SetBatchProcessor(batchProcessor)
                            .Build();
                        break;

                    case IterationMode.CRB:
                        batchProcessor = new BatchProcessor(RequestProfile, _watchdog);
                        iterationModeService = new CRBMode.Builder()
                            .SetCommand(profileCommand)
                            .SetRequestCount(this.RequestCount.Value)
                            .SetCoolDownTime(this.CoolDownTime.Value)
                            .SetBatchSize(this.BatchSize.Value)
                            .SetMaximizeThroughput(this.MaximizeThroughput)
                            .SetBatchProcessor(batchProcessor)
                            .Build();
                        break;

                    case IterationMode.CB:
                        batchProcessor = new BatchProcessor(RequestProfile, _watchdog);
                        iterationModeService = new CBMode.Builder()
                            .SetCommand(profileCommand)
                            .SetCoolDownTime(this.CoolDownTime.Value)
                            .SetBatchSize(this.BatchSize.Value)
                            .SetMaximizeThroughput(this.MaximizeThroughput)
                            .SetBatchProcessor(batchProcessor)
                            .Build();
                        break;

                    case IterationMode.R:
                        iterationModeService = new RMode.Builder()
                            .SetCommand(profileCommand)
                            .SetRequestCount(this.RequestCount.Value)
                            .SetWatchdog(_watchdog)
                            .SetRequestProfile(RequestProfile)
                            .Build();
                        break;

                    case IterationMode.D:
                        iterationModeService = new DMode.Builder()
                            .SetCommand(profileCommand)
                            .SetDuration(this.Duration.Value)
                            .SetWatchdog(_watchdog)
                            .SetRequestProfile(RequestProfile)
                            .Build();
                        break;

                    default:
                        throw new ArgumentException("Invalid iteration mode was chosen");
                }

                // Execute the iteration mode service
                if (iterationModeService != null)
                {
                    _numberOfSentRequests = await iterationModeService.ExecuteAsync(_cts.Token);
                }

                await _logger.LogAsync(_runtimeOperationIdProvider.OperationId, $"The client {_httpClientService.Id} has sent {_numberOfSentRequests} request(s) to {this.RequestProfile.URL}", LPSLoggingLevel.Verbose, _cts.Token);
                await _logger.LogAsync(_runtimeOperationIdProvider.OperationId, $"The client {_httpClientService.Id} is waiting for the {_numberOfSentRequests} request(s) to complete", LPSLoggingLevel.Verbose, _cts.Token);

            }
            catch (OperationCanceledException) when (_cts.IsCancellationRequested)
            {
                throw;
            }
            finally
            {
                profileCommand.RemoveObserver(command);
            }
        }
    }
}
