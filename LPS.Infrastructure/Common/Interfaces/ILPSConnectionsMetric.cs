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
        public Task<bool> IncreaseConnectionsCountAsync(ICancellationTokenWrapper cancellationTokenWrapper);
        public Task<bool> DecreseConnectionsCountAsync(ICancellationTokenWrapper cancellationTokenWrapper);
    }
}
