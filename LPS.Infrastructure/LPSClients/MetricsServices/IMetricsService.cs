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
        Task<bool> TryIncreaseConnectionsCountAsync(Guid sessionId, CancellationToken token);
        Task<bool> TryDecreaseConnectionsCountAsync(Guid sessionId, bool isSuccessful, CancellationToken token);
        Task<bool> TryUpdateResponseMetricsAsync(Guid sessionId, HttpResponse response, CancellationToken token);
        Task<bool> TryUpdateDataSentAsync(Guid sessionId, double dataSize, CancellationToken token);
        Task<bool> TryUpdateDataReceivedAsync(Guid sessionId, double dataSize, CancellationToken token);


    }
}
