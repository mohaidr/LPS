using LPS.Domain;
using LPS.Domain.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LPS.Infrastructure.Client
{
    public class LPSHttpClientConfiguration: ILPSHttpClientConfiguration<LPSHttpRequestProfile>
    {
        private TimeSpan _pooledConnectionLifetime;
        private TimeSpan _pooledConnectionIdleTimeout;
        private int _maxConnectionsPerServer;
        private TimeSpan _timeout;
        public LPSHttpClientConfiguration(TimeSpan pooledConnectionLifetime, TimeSpan pooledConnectionIdleTimeout,
           int maxConnectionsPerServer, TimeSpan timeout) 
        {
            _pooledConnectionLifetime = pooledConnectionLifetime;
            _pooledConnectionIdleTimeout = pooledConnectionIdleTimeout;
            _maxConnectionsPerServer = maxConnectionsPerServer;
            _timeout = timeout;
        }

       public TimeSpan PooledConnectionLifetime { get { return _pooledConnectionLifetime; } }
       public TimeSpan PooledConnectionIdleTimeout { get { return _pooledConnectionIdleTimeout; } }
       public int MaxConnectionsPerServer { get { return _maxConnectionsPerServer; } }
       public TimeSpan Timeout { get { return _timeout; } }
    }
}
