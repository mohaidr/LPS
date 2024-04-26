using LPS.Domain;
using LPS.Domain.Common.Interfaces;
using LPS.Infrastructure.Logger;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Eventing.Reader;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace LPS.Infrastructure.Client
{
    //Refactor to queue manager if more queue functionalities are needed
    public class LPSHttpClientManager : ILPSHttpClientManager<LPSHttpRequestProfile, LPSHttpResponse, ILPSClientService<LPSHttpRequestProfile, LPSHttpResponse>>
    {
        ILPSLogger _logger;
        Queue<ILPSClientService<LPSHttpRequestProfile, LPSHttpResponse>> _clientsQueue;
        ILPSRuntimeOperationIdProvider _runtimeOperationIdProvider;
        public LPSHttpClientManager(ILPSLogger logger, ILPSRuntimeOperationIdProvider runtimeOperationIdProvider)
        {
            _logger = logger;
            _runtimeOperationIdProvider = runtimeOperationIdProvider;
            _clientsQueue = new Queue<ILPSClientService<LPSHttpRequestProfile, LPSHttpResponse>>();
        }

        public ILPSClientService<LPSHttpRequestProfile, LPSHttpResponse> CreateInstance(ILPSClientConfiguration<LPSHttpRequestProfile> config)
        {
            var client = new LPSHttpClientService(config, _logger, _runtimeOperationIdProvider);
            _logger.Log(_runtimeOperationIdProvider.OperationId, $"Client with Id {client.Id} has been created", LPSLoggingLevel.Verbose);
            return client;
        }

        public void CreateAndQueueClient(ILPSClientConfiguration<LPSHttpRequestProfile> config)
        {
            var client = new LPSHttpClientService(config, _logger, _runtimeOperationIdProvider);
            _clientsQueue.Enqueue(client);
            _logger.Log( _runtimeOperationIdProvider.OperationId, $"Client with Id {client.Id} has been created and queued", LPSLoggingLevel.Verbose);
        }

        public ILPSClientService<LPSHttpRequestProfile, LPSHttpResponse> DequeueClient()
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

        public ILPSClientService<LPSHttpRequestProfile, LPSHttpResponse> DequeueClient(ILPSClientConfiguration<LPSHttpRequestProfile> config, bool byPassQueueIfEmpty )
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
                    var client = new LPSHttpClientService(config, _logger, _runtimeOperationIdProvider);
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
