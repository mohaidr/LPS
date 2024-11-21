using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using LPS.Domain.Common.Interfaces;
using LPS.Domain;
using LPS.Infrastructure.Monitoring.Metrics;
using LPS.Infrastructure.Common.Interfaces;
using System;
using LPS.Infrastructure.Logger;

namespace LPS.Infrastructure.LPSClients.MetricsServices
{
    public interface IMetricsService
    {
        Task<bool> TryIncreaseConnectionsCountAsync(Guid requestId, CancellationToken token);
        Task<bool> TryDecreaseConnectionsCountAsync(Guid requestId, bool isSuccessful, CancellationToken token);
        Task<bool> TryUpdateResponseMetricsAsync(Guid requestId, HttpResponse response, CancellationToken token);
        Task<bool> TryUpdateDataSentAsync(Guid requestId, double dataSize, CancellationToken token);
        Task<bool> TryUpdateDataReceivedAsync(Guid requestId, double dataSize, CancellationToken token);


    }
}
