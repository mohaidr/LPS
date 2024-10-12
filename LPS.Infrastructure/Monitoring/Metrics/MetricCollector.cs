using LPS.Domain;
using LPS.Domain.Common.Interfaces;
using LPS.Infrastructure.Common;
using LPS.Infrastructure.Common.Interfaces;
using LPS.Infrastructure.Monitoring.EventSources;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace LPS.Infrastructure.Monitoring.Metrics
{
    public abstract class BaseMetricCollector(HttpRun httpRun, ILogger logger, IRuntimeOperationIdProvider runtimeOperationIdProvider) : IMetricCollector
    {
        protected HttpRun _httpRun = httpRun;
        protected abstract IDimensionSet DimensionSet { get; }
        protected ILogger _logger = logger;
        protected IRuntimeOperationIdProvider _runtimeOperationIdProvider = runtimeOperationIdProvider;

        public HttpRun LPSHttpRun => _httpRun;
        public bool IsStopped { get; protected set; }

        public abstract LPSMetricType MetricType { get; }

        public string Stringify()
        {
            try
            {
                return SerializationHelper.Serialize(DimensionSet);
            }
            catch (Exception ex)
            {
                return string.Empty;
            }
        }

        public async Task<IDimensionSet> GetDimensionSetAsync()
        {
            if (DimensionSet != null)
            {
                return DimensionSet;
            }
            else
            {
                await _logger?.LogAsync(_runtimeOperationIdProvider.OperationId ?? "0000-0000-0000-0000", $"Dimension set is empty", LPSLoggingLevel.Error);
                throw new InvalidOperationException("Dimension set is empty");
            }
        }

        public async Task<TDimensionSet> GetDimensionSetAsync<TDimensionSet>() where TDimensionSet : IDimensionSet
        {
            if (DimensionSet is TDimensionSet dimensionSet)
            {
                return dimensionSet;
            }
            else
            {
                await _logger?.LogAsync(_runtimeOperationIdProvider.OperationId ?? "0000-0000-0000-0000", $"Dimension set of type {typeof(TDimensionSet)} is not supported", LPSLoggingLevel.Error);
                throw new InvalidCastException($"Dimension set of type {typeof(TDimensionSet)} is not supported");
            }
        }

        public abstract void Stop();

        public abstract void Start();
    }
}
