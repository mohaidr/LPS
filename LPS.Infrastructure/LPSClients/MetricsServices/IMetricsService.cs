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
        void AddMetrics(Guid requestId);
        Task<bool> TryIncreaseConnectionsCount(HttpRequestProfile requestProfile, CancellationToken token);
        Task<bool> TryDecreaseConnectionsCount(HttpRequestProfile requestProfile, bool isSuccessful, CancellationToken token);
        Task<bool> TryUpdateResponseMetrics(HttpRequestProfile requestProfile, HttpResponse response, CancellationToken token);
    }
}
