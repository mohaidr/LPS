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
        Task<bool> TryIncreaseConnectionsCountAsync(Guid requestProfileId, CancellationToken token);
        Task<bool> TryDecreaseConnectionsCountAsync(Guid requestProfileId, bool isSuccessful, CancellationToken token);
        Task<bool> TryUpdateResponseMetricsAsync(Guid requestProfileId, HttpResponse response, CancellationToken token);
        Task<bool> TryUpdateDataSentAsync(Guid requestId, double dataSize, CancellationToken token);
        Task<bool> TryUpdateDataReceivedAsync(Guid requestId, double dataSize, CancellationToken token);


    }
}
