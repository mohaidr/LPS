using LPS.Domain;
using LPS.Domain.Common.Interfaces;
using LPS.Infrastructure.Logger;
using LPS.Infrastructure.Monitoring.Metrics;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Eventing.Reader;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;


namespace LPS.Infrastructure.LPSClients
{
    //Refactor to queue manager if more queue functionalities are needed
    public class HttpClientManager(ILogger logger, IRuntimeOperationIdProvider runtimeOperationIdProvider, IMetricsQueryService metricsQueryService) : IHttpClientManager<HttpRequestProfile, HttpResponse, IClientService<HttpRequestProfile, HttpResponse>>
    {
        readonly ILogger _logger = logger;
        readonly Queue<IClientService<HttpRequestProfile, HttpResponse>> _clientsQueue = new Queue<IClientService<HttpRequestProfile, HttpResponse>>();
        readonly IRuntimeOperationIdProvider _runtimeOperationIdProvider = runtimeOperationIdProvider;
        readonly IMetricsQueryService _metricsQueryService = metricsQueryService;

        public IClientService<HttpRequestProfile, HttpResponse> CreateInstance(IClientConfiguration<HttpRequestProfile> config)
        {
            var client = new HttpClientService(config, _logger, _runtimeOperationIdProvider, _metricsQueryService);
            _logger.Log(_runtimeOperationIdProvider.OperationId, $"Client with Id {client.Id} has been created", LPSLoggingLevel.Verbose);
            return client;
        }

        public void CreateAndQueueClient(IClientConfiguration<HttpRequestProfile> config)
        {
            var client = new HttpClientService(config, _logger, _runtimeOperationIdProvider, _metricsQueryService);
            _clientsQueue.Enqueue(client);
            _logger.Log(_runtimeOperationIdProvider.OperationId, $"Client with Id {client.Id} has been created and queued", LPSLoggingLevel.Verbose);
        }

        public IClientService<HttpRequestProfile, HttpResponse> DequeueClient()
        {
            if (_clientsQueue.Count > 0)
            {
                var client = _clientsQueue.Dequeue();
                _logger.Log(_runtimeOperationIdProvider.OperationId, $"Client with Id {client.Id} has been dequeued", LPSLoggingLevel.Verbose);
                return client;
            }
            else
            {
                _logger.Log(_runtimeOperationIdProvider.OperationId, $"Client Queue is empty", LPSLoggingLevel.Warning);
                return null;
            }
        }

        public IClientService<HttpRequestProfile, HttpResponse> DequeueClient(IClientConfiguration<HttpRequestProfile> config, bool byPassQueueIfEmpty)
        {
            if (_clientsQueue.Count > 0)
            {
                var client = _clientsQueue.Dequeue();
                _logger.Log(_runtimeOperationIdProvider.OperationId, $"Client with Id {client.Id} was dequeued", LPSLoggingLevel.Information);
                return client;
            }
            else
            {
                if (byPassQueueIfEmpty)
                {
                    var client = new HttpClientService(config, _logger, _runtimeOperationIdProvider, _metricsQueryService);
                    _logger.Log(_runtimeOperationIdProvider.OperationId, $"Queue was empty but a client with Id {client.Id} was created", LPSLoggingLevel.Information);
                    return client;
                }
                else
                {
                    _logger.Log(_runtimeOperationIdProvider.OperationId, $"Client Queue is empty", LPSLoggingLevel.Warning);
                    return null;
                }
            }
        }
    }
}
