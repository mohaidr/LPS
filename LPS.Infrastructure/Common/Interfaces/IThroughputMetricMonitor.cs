using LPS.Domain;
using LPS.Domain.Common.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LPS.Infrastructure.Common.Interfaces
{
    public interface IThroughputMetricMonitor : IMetricMonitor
    {
        public bool IncreaseConnectionsCount();
        public bool DecreseConnectionsCount(bool isSuccess);
    }
}
