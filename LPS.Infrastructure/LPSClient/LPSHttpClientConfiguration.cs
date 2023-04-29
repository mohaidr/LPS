using LPS.Domain;
using LPS.Domain.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LPS.Infrastructure.Client
{
    public class LPSHttpClientConfiguration: ILPSHttpClientConfiguration<LPSHttpRequest>
    {
       public TimeSpan PooledConnectionLifetime { get; set; }
       public TimeSpan PooledConnectionIdleTimeout { get; set; }
       public int MaxConnectionsPerServer { get; set; }
       public TimeSpan Timeout { get; set; }
    }
}
