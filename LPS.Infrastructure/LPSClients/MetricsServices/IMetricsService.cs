using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using LPS.Domain.Common.Interfaces;
using LPS.Domain;
using LPS.Infrastructure.Monitoring.Metrics;
using LPS.Infrastructure.Common.Interfaces;
using System;

namespace LPS.Infrastructure.LPSClients.MetricsServices
{
    public interface IMetricsService
    {
        Task AddMetricsAsync(Guid requestId);
        Task<bool> TryIncreaseConnectionsCountAsync(HttpRequestProfile requestProfile, CancellationToken token);
        Task<bool> TryDecreaseConnectionsCountAsync(HttpRequestProfile requestProfile, bool isSuccessful, CancellationToken token);
        Task<bool> TryUpdateResponseMetricsAsync(HttpRequestProfile requestProfile, HttpResponse response, CancellationToken token);
    }
}
