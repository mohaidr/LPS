using LPS.Domain;
using LPS.Domain.Common.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LPS.Infrastructure.Common.Interfaces
{
    public interface ILPSConnectionsMetric : ILPSMetric
    {
        public bool IncreaseConnectionsCount(ICancellationTokenWrapper cancellationTokenWrapper);
        public bool DecreseConnectionsCount(ICancellationTokenWrapper cancellationTokenWrapper);
    }
}
