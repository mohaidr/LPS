using System;
using System.Threading;
using System.Threading.Tasks;
using LPS.Domain.Common.Interfaces;
using LPS.Infrastructure.Common.Interfaces;

namespace LPS.Infrastructure.Monitoring.MetricsServices
{
    public class SkippedRequestReporter(IMetricsService metricsService) : ISkippedRequestReporter
    {
        private readonly IMetricsService _metricsService = metricsService;

        public ValueTask<bool> ReportSkippedRequestAsync(Guid requestId, CancellationToken token = default)
        {
            return _metricsService.TryIncreaseSkippedRequestsCountAsync(requestId, token);
        }
    }
}
