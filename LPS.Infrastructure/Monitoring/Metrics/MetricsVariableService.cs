#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;
using LPS.Domain.Domain.Common.Enums;
using LPS.Domain.Common.Interfaces;
using LPS.Infrastructure.VariableServices.GlobalVariableManager;
using LPS.Infrastructure.VariableServices.VariableHolders;
using LPS.Infrastructure.Common.Interfaces;
using AsyncKeyedLock;

namespace LPS.Infrastructure.Monitoring.MetricsVariables
{
    public sealed class MetricsVariableService : IMetricsVariableService
    {
        private const string MetricsRootName = "Metrics";

        private readonly IVariableManager _manager;
        private readonly IPlaceholderResolverService _resolver;
        private readonly ILogger _logger;
        private readonly IRuntimeOperationIdProvider _op;

        private static readonly AsyncKeyedLocker<string> _locker = new();

        public MetricsVariableService(
            IVariableManager manager,
            IPlaceholderResolverService resolver,
            ILogger logger,
            IRuntimeOperationIdProvider op)
        {
            _manager = manager ?? throw new ArgumentNullException(nameof(manager));
            _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _op = op ?? throw new ArgumentNullException(nameof(op));
        }

        public async Task PutMetricAsync(string iterationName, string metricName, string dimensionSetJson, CancellationToken token)
        {
            if (string.IsNullOrWhiteSpace(iterationName))
                throw new ArgumentException("Iteration name is required.", nameof(iterationName));
            if (string.IsNullOrWhiteSpace(metricName))
                throw new ArgumentException("Metric name is required.", nameof(metricName));
            if (dimensionSetJson is null)
                throw new ArgumentNullException(nameof(dimensionSetJson));

            token.ThrowIfCancellationRequested();

            var metricsRoot = await _manager.GetAsync(MetricsRootName, token).ConfigureAwait(false) as MultipleVariableHolder;
            if (metricsRoot is null)
            {
                var newRoot = (MultipleVariableHolder)await new MultipleVariableHolder.VBuilder(_resolver, _logger, _op, _manager)
                    .SetGlobal(true)
                    .BuildAsync(token)
                    .ConfigureAwait(false);

                await _manager.PutAsync(MetricsRootName, newRoot, token).ConfigureAwait(false);
            }

            using (await _locker.LockAsync(iterationName, token).ConfigureAwait(false))
            {
                token.ThrowIfCancellationRequested();

                metricsRoot = await _manager.GetAsync(MetricsRootName, token).ConfigureAwait(false) as MultipleVariableHolder;
                if (metricsRoot is null)
                {
                    metricsRoot = (MultipleVariableHolder)await new MultipleVariableHolder.VBuilder(_resolver, _logger, _op, _manager)
                        .SetGlobal(true)
                        .BuildAsync(token)
                        .ConfigureAwait(false);

                    await _manager.PutAsync(MetricsRootName, metricsRoot, token).ConfigureAwait(false);
                }

                MultipleVariableHolder iterationContainer;
                WrapperVariableHolder iterationWrapper;

                if (metricsRoot.TryGetChild(iterationName, out var existingIteration) &&
                    existingIteration is IWrapperVariableHolder wrapper &&
                    wrapper.VariableHolder is MultipleVariableHolder innerContainer)
                {
                    iterationWrapper = (WrapperVariableHolder)wrapper;
                    iterationContainer = innerContainer;
                }
                else
                {
                    iterationContainer = (MultipleVariableHolder)await new MultipleVariableHolder.VBuilder(_resolver, _logger, _op, _manager)
                        .SetGlobal()
                        .BuildAsync(token)
                        .ConfigureAwait(false);

                    iterationWrapper = (WrapperVariableHolder)await new WrapperVariableHolder.VBuilder(_resolver, _logger, _op)
                        .WithVariable(iterationContainer)
                        .SetGlobal()
                        .BuildAsync(token)
                        .ConfigureAwait(false);

                    await ((MultipleVariableHolder.VBuilder)metricsRoot.Builder)
                        .AttachChild(iterationName, iterationWrapper)
                        .BuildAsync(token)
                        .ConfigureAwait(false);
                }

                if (iterationContainer.TryGetChild(metricName, out var existingMetric) &&
                    existingMetric is StringVariableHolder svh)
                {
                    await ((StringVariableHolder.VBuilder)svh.Builder)
                        .WithType(VariableType.JsonString)
                        .WithPattern(string.Empty)
                        .WithRawValue(dimensionSetJson)
                        .SetGlobal()
                        .BuildAsync(token)
                        .ConfigureAwait(false);
                }
                else
                {
                    var metricHolder = await new StringVariableHolder.VBuilder(_resolver, _logger, _op)
                        .WithType(VariableType.JsonString)
                        .WithPattern(string.Empty)
                        .WithRawValue(dimensionSetJson)
                        .SetGlobal()
                        .BuildAsync(token)
                        .ConfigureAwait(false);

                    await ((MultipleVariableHolder.VBuilder)iterationContainer.Builder)
                        .AttachChild(metricName, metricHolder)
                        .BuildAsync(token)
                        .ConfigureAwait(false);
                }
            }
        }
    }
}
