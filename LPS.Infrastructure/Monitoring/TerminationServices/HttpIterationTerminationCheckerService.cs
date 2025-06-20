using LPS.Domain;
using LPS.Domain.Domain.Common.Interfaces;
using LPS.Infrastructure.Common.Interfaces;
using LPS.Infrastructure.Monitoring.Metrics;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace LPS.Infrastructure.Monitoring.TerminationServices
{
    public class HttpIterationTerminationCheckerService : ITerminationCheckerService
    {
        private readonly IMetricsQueryService _metricsQueryService;
        private readonly ConcurrentDictionary<(Guid, TerminationRule), GracePeriodState> _state = new();

        public HttpIterationTerminationCheckerService(IMetricsQueryService metricsQueryService)
        {
            _metricsQueryService = metricsQueryService;
        }

        public async Task<bool> IsTerminationRequiredAsync(Iteration iteration)
        {
            if (iteration is not HttpIteration httpIteration)
                throw new ArgumentException("Expected HttpIteration", nameof(iteration));

            if (httpIteration.TerminationRules == null || !httpIteration.TerminationRules.Any())
                return false;

            var collector = (await _metricsQueryService.GetAsync<ResponseCodeMetricCollector>(
                c => c.HttpIteration.Id == httpIteration.Id)).SingleOrDefault();

            if (collector == null ||
                (await collector.GetDimensionSetAsync()) is not ResponseCodeMetricDimensionSet dimSet)
                return false;

            foreach (var rule in httpIteration.TerminationRules)
            {
                if (rule.MaxErrorRate is null || rule.GracePeriod is null)
                    continue;

                var key = (httpIteration.Id, rule);
                var state = _state.GetOrAdd(key, _ => new GracePeriodState(rule.GracePeriod.Value));

                int total = 0;
                int errors = 0;

                foreach (var summary in dimSet.ResponseSummaries)
                {
                    int count = summary.Count;
                    var code = (HttpStatusCode)int.Parse(summary.HttpStatusCode);

                    total += count;
                    if (rule.ErrorStatusCodes.Contains(code))
                        errors += count;
                }

                if (await state.UpdateAndCheckAsync(total, errors, rule.MaxErrorRate.Value))
                    return true;
            }

            return false;
        }
    }
}

