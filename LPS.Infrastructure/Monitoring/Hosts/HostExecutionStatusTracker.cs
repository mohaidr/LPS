#nullable enable
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using LPS.Domain;
using LPS.Domain.Domain.Common.Enums;
using LPS.Domain.Domain.Common.Extensions;
using LPS.Domain.Domain.Common.Interfaces;

namespace LPS.Infrastructure.Monitoring.Hosts
{
    internal sealed class HostExecutionStatusTracker
    {
        private readonly ConcurrentDictionary<HostKey, ConcurrentDictionary<System.Guid, System.Func<EntityExecutionStatus>>> _hosts = new();
        private readonly IIterationStatusMonitor? _statusMonitor;

        public HostExecutionStatusTracker(IIterationStatusMonitor? statusMonitor)
        {
            _statusMonitor = statusMonitor;
        }

        internal void Track(HostKey hostKey, HttpIteration iteration)
        {
            if (_statusMonitor == null) return;

            Track(
                hostKey,
                iteration.Id,
                () => _statusMonitor.GetTerminalStatusAsync(iteration, CancellationToken.None)
                    .AsTask().GetAwaiter().GetResult());
        }

        internal void Track(
            HostKey hostKey,
            System.Guid iterationId,
            System.Func<EntityExecutionStatus> getStatus) =>
            _hosts.GetOrAdd(hostKey, static _ => new()).TryAdd(iterationId, getStatus);

        internal HostExecutionStatus GetStatus(HostKey hostKey)
        {
            if (!_hosts.TryGetValue(hostKey, out var statuses) || statuses.IsEmpty)
                return HostExecutionStatus.Ongoing;

            var currentStatuses = statuses.Values
                .Select(getStatus => getStatus())
                .ToArray();

            return currentStatuses.All(status => status.IsTerminal())
                ? HostExecutionStatus.Completed
                : HostExecutionStatus.Ongoing;
        }
    }

    internal enum HostExecutionStatus
    {
        Ongoing,
        Completed
    }
}