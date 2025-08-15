#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;
using AsyncKeyedLock;
using LPS.Domain.Common.Interfaces;
using LPS.Domain.Domain.Common.Enums;
using LPS.Infrastructure.Common.Interfaces;
using LPS.Infrastructure.VariableServices.GlobalVariableManager;
using LPS.Infrastructure.VariableServices.VariableHolders;

namespace LPS.Infrastructure.Monitoring.MetricsVariables
{

    public sealed class MetricsVariableService : IMetricsVariableService
    {
        private const string MetricsRootName = "Metrics";

        private readonly IVariableManager _manager;
        private readonly IPlaceholderResolverService _resolver;
        private readonly ILogger _logger;
        private readonly IRuntimeOperationIdProvider _op;

        // lock key = $"{roundName}::{iterationName}"
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

        public async Task PutMetricAsync(
            string roundName,
            string iterationName,
            string metricName,
            string dimensionSetJson,
            CancellationToken token)
        {
            if (string.IsNullOrWhiteSpace(roundName))
                throw new ArgumentException("Round name is required.", nameof(roundName));
            if (string.IsNullOrWhiteSpace(iterationName))
                throw new ArgumentException("Iteration name is required.", nameof(iterationName));
            if (string.IsNullOrWhiteSpace(metricName))
                throw new ArgumentException("Metric name is required.", nameof(metricName));
            if (dimensionSetJson is null)
                throw new ArgumentNullException(nameof(dimensionSetJson));

            token.ThrowIfCancellationRequested();

 
            // lock per (round,iteration) to avoid cross-writes
            var lockKey = $"{roundName}::{iterationName}";
            using (await _locker.LockAsync(lockKey, token).ConfigureAwait(false))
            {
                token.ThrowIfCancellationRequested();

                // refresh root under lock in case it was created concurrently
                var metricsRoot = await GetOrCreateRootAsync(token).ConfigureAwait(false);

                // ensure round node
                var roundNode = await GetOrCreateChildContainerAsync(metricsRoot, roundName, token).ConfigureAwait(false);

                // ensure iteration node under round
                var iterationNode = await GetOrCreateChildContainerAsync(roundNode, iterationName, token).ConfigureAwait(false);

                // upsert metric as JsonString
                if (iterationNode.TryGetChild(metricName, out var existing) && existing is StringVariableHolder s)
                {
                    await ((StringVariableHolder.VBuilder)s.Builder)
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

                    await ((MultipleVariableHolder.VBuilder)iterationNode.Builder)
                        .AttachChild(metricName, metricHolder)
                        .BuildAsync(token)
                        .ConfigureAwait(false);
                }
            }
        }

        private async Task<MultipleVariableHolder> GetOrCreateRootAsync(CancellationToken token)
        {
            var root = await _manager.GetAsync(MetricsRootName, token).ConfigureAwait(false) as MultipleVariableHolder;
            if (root is not null) return root;

            var newRoot = (MultipleVariableHolder)await new MultipleVariableHolder.VBuilder(_resolver, _logger, _op, _manager)
                .SetGlobal()
                .BuildAsync(token)
                .ConfigureAwait(false);

            await _manager.PutAsync(MetricsRootName, newRoot, token).ConfigureAwait(false);
            return newRoot;
        }

        private  async Task<MultipleVariableHolder> GetOrCreateChildContainerAsync(
            MultipleVariableHolder parent,
            string childName,
            CancellationToken token)
        {
            if (parent.TryGetChild(childName, out var existing) && existing is MultipleVariableHolder m) return m;

            var newContainer = (MultipleVariableHolder)await new MultipleVariableHolder.VBuilder(
                    _resolver, _logger, _op, _manager)
                .SetGlobal()
                .BuildAsync(token)
                .ConfigureAwait(false);

            await ((MultipleVariableHolder.VBuilder)parent.Builder)
                .AttachChild(childName, newContainer)
                .BuildAsync(token)
                .ConfigureAwait(false);

            return newContainer;
        }
    }
}
