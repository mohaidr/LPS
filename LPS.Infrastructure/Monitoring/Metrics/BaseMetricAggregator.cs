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
    public abstract class BaseMetricAggregator(HttpIteration httpIteration, ILogger logger, IRuntimeOperationIdProvider runtimeOperationIdProvider) : IMetricAggregator
    {
        protected HttpIteration _httpIteration = httpIteration;
        protected abstract IMetricShapshot Snapshot { get; }
        protected ILogger _logger = logger;
        protected IRuntimeOperationIdProvider _runtimeOperationIdProvider = runtimeOperationIdProvider;

        public HttpIteration HttpIteration => _httpIteration;
        public bool IsStarted { get; protected set; }

        public abstract LPSMetricType MetricType { get; }

        public string Stringify()
        {
            try
            {
                return SerializationHelper.Serialize(Snapshot);
            }
            catch (Exception ex)
            {
                return string.Empty;
            }
        }

        public async ValueTask<IMetricShapshot> GetSnapshotAsync(CancellationToken token)
        {
            if (Snapshot != null)
            {
                return Snapshot;
            }
            else
            {
                await _logger?.LogAsync(_runtimeOperationIdProvider.OperationId ?? "0000-0000-0000-0000", $"Dimension set is empty", LPSLoggingLevel.Error, token);
                throw new InvalidOperationException("Dimension set is empty");
            }
        }

        public async ValueTask<TDimensionSet> GetSnapshotAsync<TDimensionSet>(CancellationToken token) where TDimensionSet : IMetricShapshot
        {
            if (Snapshot is TDimensionSet dimensionSet)
            {
                return dimensionSet;
            }
            else
            {
                await _logger?.LogAsync(_runtimeOperationIdProvider.OperationId ?? "0000-0000-0000-0000", $"Dimension set of type {typeof(TDimensionSet)} is not supported", LPSLoggingLevel.Error, token);
                throw new InvalidCastException($"Dimension set of type {typeof(TDimensionSet)} is not supported");
            }
        }

        public abstract void Stop();

        public abstract void Start();
    }
}
