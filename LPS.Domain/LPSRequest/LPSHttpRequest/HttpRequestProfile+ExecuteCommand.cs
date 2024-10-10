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
using LPS.Domain.Domain.Common.Interfaces;

namespace LPS.Domain
{

    public partial class HttpRequestProfile
    {
        readonly SemaphoreSlim _semaphoreSlim = new(1, 1);
        public class ExecuteCommand(IClientService<HttpRequestProfile,
                HttpResponse> httpClientService,
            ILogger logger,
            IWatchdog watchdog,
            IRuntimeOperationIdProvider runtimeOperationIdProvider,
            CancellationTokenSource cts) : IAsyncCommand<HttpRequestProfile>, IStateSubject
        {
            private IClientService<HttpRequestProfile, HttpResponse> _httpClientService { get; set; } = httpClientService;
            readonly ILogger _logger = logger;
            readonly IWatchdog _watchdog = watchdog;
            readonly IRuntimeOperationIdProvider _runtimeOperationIdProvider = runtimeOperationIdProvider;
            readonly CancellationTokenSource _cts = cts;
            private CommandExecutionStatus _executionStatus;
            private CommandExecutionStatus _aggregateStatus;
            public CommandExecutionStatus Status => _executionStatus;
            public CommandExecutionStatus AggregateStatus => _aggregateStatus;
            readonly SemaphoreSlim _semaphoreSlim = new(1, 1);
            async public Task ExecuteAsync(HttpRequestProfile entity)
            {
                try
                {
                    if (entity == null)
                    {
                        _logger.Log(_runtimeOperationIdProvider.OperationId, "LPSHttpRequestProfile Entity Must Have a Value", LPSLoggingLevel.Error);
                        throw new ArgumentNullException(nameof(entity));
                    }
                    entity._httpClientService = this._httpClientService;
                    entity._logger = this._logger;
                    entity._watchdog = this._watchdog;
                    entity._runtimeOperationIdProvider = this._runtimeOperationIdProvider;
                    entity._cts = this._cts;
                    _executionStatus = CommandExecutionStatus.Ongoing;
                    await entity.ExecuteAsync(this);
                    _executionStatus = CommandExecutionStatus.Completed;
                }
                catch {
                    _executionStatus = CommandExecutionStatus.Failed;
                }
                finally {
                    await _semaphoreSlim.WaitAsync();
                    if (_aggregateStatus < _executionStatus)
                    {
                        _aggregateStatus = _executionStatus;
                        NotifyObservers();
                    }
                    _semaphoreSlim.Release();
                }
            }
            private List<IStateObserver> _observers = new List<IStateObserver>();

            public void RegisterObserver(IStateObserver observer)
            {
                _observers.Add(observer);
            }

            public void RemoveObserver(IStateObserver observer)
            {
                _observers.Remove(observer);
            }

            public void NotifyObservers()
            {
                foreach (var observer in _observers)
                {
                    observer.NotifyMe(_aggregateStatus);
                }
            }
        }

        async private Task ExecuteAsync(ExecuteCommand command)
        {
            if (this.IsValid)
            {
                string hostName = new Uri(this.URL).Host;

                await _watchdog.BalanceAsync(hostName, _cts.Token);
                /* 
                 * Clone the entity so we send a different entity to the http client service.
                 * To avoid writing to the same instnace concurrently where we update the sequence number which is used by the http client service, so if the sequence number changes while used by the http client service, then a wrong sequence number will be used and may result in exceptions or unexpected behaviors
                 * This logic may change in the future when we refactor the http client service
                */
                var clonedEntity = this.Clone();
                try
                {
                    if (this._httpClientService == null)
                    {
                        throw new InvalidOperationException("Http Client Is Not Defined");
                    }
                    await _semaphoreSlim.WaitAsync(_cts.Token);
                    int sequenceNumber = ++this.LastSequenceId;
                    ((HttpRequestProfile)clonedEntity).LastSequenceId = sequenceNumber;
                    _semaphoreSlim.Release();
                    await _httpClientService.SendAsync((HttpRequestProfile)clonedEntity, _cts.Token);
                    this.HasFailed = false;
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
