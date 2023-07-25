using LPS.Domain;
using LPS.Domain.Common;
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
    public class LPSHttpClientManager : ILPSHttpClientManager<LPSHttpRequest, ILPSClientService<LPSHttpRequest>>
    {
        ILPSLogger _logger;
        Queue<ILPSClientService<LPSHttpRequest>> _clientsQueue;
        IRuntimeOperationIdProvider _runtimeOperationIdProvider;
        public LPSHttpClientManager(ILPSLogger logger, IRuntimeOperationIdProvider runtimeOperationIdProvider)
        {
            _logger = logger;
            _runtimeOperationIdProvider = runtimeOperationIdProvider;
            _clientsQueue = new Queue<ILPSClientService<LPSHttpRequest>>();
        }

        public ILPSClientService<LPSHttpRequest> CreateInstance(ILPSClientConfiguration<LPSHttpRequest> config)
        {
            var client = new LPSHttpClientService(config, _logger, _runtimeOperationIdProvider);
            _logger.Log(_runtimeOperationIdProvider.OperationId, $"Client with Id {client.Id} has been created", LPSLoggingLevel.Information);
            return client;
        }

        public void CreateAndQueueClient(ILPSClientConfiguration<LPSHttpRequest> config)
        {
            var client = new LPSHttpClientService(config, _logger, _runtimeOperationIdProvider);
            _clientsQueue.Enqueue(client);
            _logger.Log( _runtimeOperationIdProvider.OperationId, $"Client with Id {client.Id} has been created and queued", LPSLoggingLevel.Information);
        }

        public ILPSClientService<LPSHttpRequest> DequeueClient()
        {
            if (_clientsQueue.Count > 0)
            {
                var client = _clientsQueue.Dequeue();
                _logger.Log(_runtimeOperationIdProvider.OperationId, $"Client with Id {client.Id} has been dequeued", LPSLoggingLevel.Information);
                return client;
            }
            else
            {
                _logger.Log(_runtimeOperationIdProvider.OperationId, $"Client Queue is empty", LPSLoggingLevel.Warning);
                return null;
            }
        }

        public ILPSClientService<LPSHttpRequest> DequeueClient(ILPSClientConfiguration<LPSHttpRequest> config, bool byPassQueueIfEmpty )
        {
            var stopWatch = new Stopwatch();
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
