using LPS.Domain;
using LPS.Domain.Common;
using System;
using System.Collections;
using System.Collections.Generic;
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
        public LPSHttpClientManager(ILPSLogger logger)
        {
            _logger = logger;
            _clientsQueue = new Queue<ILPSClientService<LPSHttpRequest>>();
        }

        public ILPSClientService<LPSHttpRequest> CreateInstance(ILPSClientConfiguration<LPSHttpRequest> config)
        {
            return new LPSHttpClientService(config, _logger);
        }

        public void CreateAndQueueClient(ILPSClientConfiguration<LPSHttpRequest> config)
        {
            _clientsQueue.Enqueue(new LPSHttpClientService(config, _logger));
        }

        public ILPSClientService<LPSHttpRequest> DequeueClient()
        {
            if (_clientsQueue.Count > 0)
            {
                return _clientsQueue.Dequeue();
            }
            else
            {
                return null;
            }
        }

        public ILPSClientService<LPSHttpRequest> DequeueClient(ILPSClientConfiguration<LPSHttpRequest> config, bool byPassQueueIfEmpty )
        {
            if (_clientsQueue.Count > 0)
            {
                return _clientsQueue.Dequeue();
            }
            else
            {
                if(byPassQueueIfEmpty)
                    return new LPSHttpClientService(config, _logger);
                else
                    return null;
            }
        }
    }
}
