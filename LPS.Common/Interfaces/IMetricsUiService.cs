using LPS.Infrastructure.Common.Interfaces;
using LPS.UI.Common.DTOs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LPS.Common.Interfaces
{
    public sealed record MetricsQuery(
    string? RoundName = null,
    Guid? IterationId = null,
    LPSMetricType? MetricType = null);
    public interface IMetricsUiService
    {
        Task<IReadOnlyList<MetricDataDto>> QueryAsync(MetricsQuery query, CancellationToken token = default);
        Task<IReadOnlyList<MetricDataDto>> LatestForIterationAsync(Guid iterationId, CancellationToken token = default);
        Task<IReadOnlyList<MetricDataDto>> LatestForRoundAsync(string roundName, CancellationToken token = default);
        Task<IReadOnlyList<MetricDataDto>> LatestByMetricTypeAsync(LPSMetricType metricType, CancellationToken token = default);
    }
}
