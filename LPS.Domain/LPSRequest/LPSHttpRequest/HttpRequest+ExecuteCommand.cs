using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using LPS.Domain.Common.Interfaces;
using LPS.Domain.Domain.Common.Enums;
using LPS.Domain.Domain.Common.Interfaces;

namespace LPS.Domain
{

    public partial class HttpRequest
    {
        public class ExecuteCommand(IClientService<HttpRequest, HttpResponse> httpClientService,
            ISkippedRequestReporter skippedRequestReporter,
            ILogger logger,
            IWatchdog watchdog,
            IRuntimeOperationIdProvider runtimeOperationIdProvider,
            IIfEvaluator ifEvaluator) : IAsyncCommand<HttpRequest>
        {
            private IClientService<HttpRequest, HttpResponse> _httpClientService { get; set; } = httpClientService;
            private readonly ISkippedRequestReporter _skippedRequestReporter = skippedRequestReporter;
            public IClientService<HttpRequest, HttpResponse> HttpClientService => _httpClientService;
            readonly ILogger _logger = logger;
            readonly IWatchdog _watchdog = watchdog;
            readonly IRuntimeOperationIdProvider _runtimeOperationIdProvider = runtimeOperationIdProvider;
            readonly IIfEvaluator _ifEvaluator = ifEvaluator;
            private CommandExecutionStatus _executionStatus;
            public CommandExecutionStatus Status => _executionStatus;
            //TODO: This one method and the calsses uses it are tightly coupled (behavioral coupling)
            //and need to clean it up and use clear contracts as any change in the logic here will break
            //the system 
            async public Task ExecuteAsync(HttpRequest entity, CancellationToken token)
            {
                try
                {
                    if (entity == null)
                    {
                        _logger.Log(_runtimeOperationIdProvider.OperationId, "HttpRequest Entity Must Have a Value", LPSLoggingLevel.Error);
                        throw new ArgumentNullException(nameof(entity));
                    }
                    entity._logger = this._logger;
                    entity._watchdog = this._watchdog;
                    entity._runtimeOperationIdProvider = this._runtimeOperationIdProvider;

                    if (await _ifEvaluator.EvaluateAsync(entity.SkipIf, _httpClientService.SessionId, token))
                    {
                        await _skippedRequestReporter.ReportSkippedRequestAsync(entity.Id, token);

                        await _logger.LogAsync(_runtimeOperationIdProvider.OperationId, $"Request for URL {entity.Url.Url} has been skipped because the condition '{entity.SkipIf}' evaluated to true.", LPSLoggingLevel.Warning, token);
                        _executionStatus = CommandExecutionStatus.Skipped;
                        return;
                    }

                    _executionStatus = CommandExecutionStatus.Ongoing;

                    int attempt = 0;
                    while (true)
                    {
                        attempt++;
                        try
                        {
                            await entity.ExecuteAsync(this, token);
                        }
                        catch (OperationCanceledException) when (token.IsCancellationRequested)
                        {
                            throw;
                        }
                        catch
                        {
                            entity.HasFailed = true;
                        }

                        bool shouldRetry = await ShouldRetryAsync(entity, token);
                        bool hasRetriesLeft = attempt <= (entity.Retry?.MaxRetries ?? 0);

                        if (!shouldRetry || !hasRetriesLeft)
                            break;

                        int delayInMs = CalculateRetryDelayInMs(entity, attempt);
                        await _logger.LogAsync(
                            _runtimeOperationIdProvider.OperationId,
                            $"RetryIf for URL {entity.Url.Url} evaluated to true. Retrying attempt {attempt}/{entity.Retry?.MaxRetries ?? 0} in {delayInMs} ms.",
                            LPSLoggingLevel.Warning,
                            token);

                        await Task.Delay(delayInMs, token);
                    }

                    if (!entity.HasFailed)
                        _executionStatus = CommandExecutionStatus.Completed;
                    else
                        _executionStatus = CommandExecutionStatus.Failed;
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested)
                {
                    throw;
                }
                catch
                {
                    _executionStatus = CommandExecutionStatus.Failed;
                }
            }

            private async Task<bool> ShouldRetryAsync(HttpRequest entity, CancellationToken token)
            {
                string stopIf = entity.Retry?.StopIf;
                if (!string.IsNullOrWhiteSpace(stopIf))
                {
                    bool stopRetries = await _ifEvaluator.EvaluateAsync(stopIf, _httpClientService.SessionId, token);
                    if (stopRetries)
                    {
                        await _logger.LogAsync(
                            _runtimeOperationIdProvider.OperationId,
                            $"Retries for URL {entity.Url.Url} were stopped because stopIf condition '{stopIf}' evaluated to true.",
                            LPSLoggingLevel.Warning,
                            token);
                        return false;
                    }
                }

                string retryIf = entity.Retry?.If;
                if (string.IsNullOrWhiteSpace(retryIf) || (entity.Retry?.MaxRetries ?? 0) <= 0)
                    return false;

                return await _ifEvaluator.EvaluateAsync(retryIf, _httpClientService.SessionId, token);
            }

            /// <summary>
            /// Calculates retry delay according to configured strategy.
            /// - DelayInMs defaults to 100ms when omitted.
            /// - Strategy defaults to Fixed.
            /// - MaxDelayInMs is only applied for Exponential strategy.
            /// </summary>
            private static int CalculateRetryDelayInMs(HttpRequest entity, int retryAttempt)
            {
                RetryDelayStrategy strategy = entity.Retry?.Strategy ?? RetryDelayStrategy.Fixed;
                bool hasMax = entity.Retry?.MaxDelayInMs.HasValue == true;

                int delay = entity.Retry?.DelayInMs ?? 100;

                if (strategy == RetryDelayStrategy.Fixed)
                    return delay;

                int exponent = Math.Max(0, retryAttempt - 1);
                double rawDelay = delay * Math.Pow(2, exponent);
                if (rawDelay > int.MaxValue) rawDelay = int.MaxValue;

                if (!hasMax)
                    return (int)rawDelay;

                return (int)Math.Min(entity.Retry.MaxDelayInMs!.Value, rawDelay);
            }
        }

        async private Task ExecuteAsync(ExecuteCommand command, CancellationToken token)
        {
            if (this.IsValid)
            {
                string hostName = this.Url.HostName;
                try
                {
                    await _watchdog.BalanceAsync(hostName, token);
                    if (command.HttpClientService == null)
                    {
                        throw new InvalidOperationException("Http Client Is Not Defined");
                    }

                    var response = await command.HttpClientService.SendAsync(this, token);
                    if (response.IsSuccessStatusCode)
                        this.HasFailed = false; // HasFailed is not valid property here, think of this as an entity you just fetch from DB to execute, so this has to change
                    else
                        this.HasFailed = true;
                }
                catch
                {
                    this.HasFailed = true;
                    throw;
                }
            }
        }
    }
}
