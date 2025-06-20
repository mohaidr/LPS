using LPS.Domain.Domain.Common.Interfaces;
using LPS.Domain;
using LPS.Infrastructure.Common.Interfaces;
using LPS.Infrastructure.Monitoring.Metrics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace LPS.Infrastructure.FailureEvaluator
{
    public class IterationFailureEvaluator : IIterationFailureEvaluator
    {
        private readonly IMetricsQueryService _metricsQueryService;

        public IterationFailureEvaluator(IMetricsQueryService metricsQueryService)
        {
            _metricsQueryService = metricsQueryService;
        }

        public async Task<bool> IsErrorRateExceededAsync(HttpIteration iteration)
        {
            if (iteration.MaxErrorRate <= 0 || iteration.ErrorStatusCodes == null)
                return false;

            var collector = (await _metricsQueryService.GetAsync<ResponseCodeMetricCollector>(
                c => c.HttpIteration.Id == iteration.Id)).SingleOrDefault();

            if (collector == null ||
                (await collector.GetDimensionSetAsync()) is not ResponseCodeMetricDimensionSet dimSet)
                return false;

            int total = 0;
            int errors = 0;

            foreach (var summary in dimSet.ResponseSummaries)
            {
                int count = summary.Count;
                var code = (HttpStatusCode)int.Parse(summary.HttpStatusCode);

                total += count;
                if (iteration.ErrorStatusCodes.Contains(code))
                    errors += count;
            }

            if (total == 0)
                return false;

            var actualErrorRate = (double)errors / total;
            return actualErrorRate > (iteration.MaxErrorRate / 100.0);
        }
    }

}
