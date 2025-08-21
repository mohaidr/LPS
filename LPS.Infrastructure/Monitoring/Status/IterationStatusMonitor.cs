using LPS.Domain;
using LPS.Domain.Domain.Common.Enums;
using LPS.Domain.Domain.Common.Interfaces;
using LPS.Protos.Shared;
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace LPS.Infrastructure.Monitoring.Status
{
    // The last update we need here, is if this is a worker, then we should call the service on the master
    public class IterationStatusMonitor : IIterationStatusMonitor
    {
        private readonly ITerminationCheckerService _terminationChecker;
        private readonly IIterationFailureEvaluator _iterationFailureEvaluator;
        private readonly CancellationTokenSource _globalCts;
        private readonly ICommandStatusMonitor<HttpIteration> _commandStatusMonitor;

        // Cache only terminal statuses: Skipped, Failed, Success, Terminated
        private static readonly ConcurrentDictionary<Guid, EntityExecutionStatus> _terminalStatusCache = new();

        public IterationStatusMonitor(
            ITerminationCheckerService terminationChecker,
            IIterationFailureEvaluator iterationFailureEvaluator,
            ICommandStatusMonitor<HttpIteration> commandStatusMonitor,
            CancellationTokenSource globalCts)
        {
            _terminationChecker = terminationChecker;
            _iterationFailureEvaluator = iterationFailureEvaluator;
            _commandStatusMonitor = commandStatusMonitor;
            _globalCts = globalCts;
        }

        public async ValueTask<EntityExecutionStatus> GetTerminalStatusAsync(HttpIteration httpIteration, CancellationToken token = default)
        {
            // 1) If we already know this iteration's terminal status, return it immediately.
            if (TryGetCachedTerminal(httpIteration, out var cached))
                return cached;

            // 2) Evaluate "Terminated" and cache if so.
            if (await _terminationChecker.IsTerminationRequiredAsync(httpIteration, token))
                return CacheAndReturn(httpIteration, EntityExecutionStatus.Terminated);

            // 3) Evaluate "Failed" (error rate exceeded) and cache if so.
            if (await _iterationFailureEvaluator.IsErrorRateExceededAsync(httpIteration, token))
                return CacheAndReturn(httpIteration, EntityExecutionStatus.Failed);

            // 4) Aggregate command statuses
            var commandsStatuses = await _commandStatusMonitor.QueryAsync(httpIteration);

            // Skipped (all skipped) -> terminal
            if (commandsStatuses.Count != 0 && commandsStatuses.All(status => status == CommandExecutionStatus.Skipped))
                return CacheAndReturn(httpIteration, EntityExecutionStatus.Skipped);

            // PartiallySkipped -> not terminal
            if (commandsStatuses.Any(status => status == CommandExecutionStatus.Skipped) &&
                !commandsStatuses.All(status => status == CommandExecutionStatus.Skipped))
                return EntityExecutionStatus.PartiallySkipped;

            // Ongoing (not terminal)
            if (commandsStatuses.Any(status => status == CommandExecutionStatus.Ongoing) && !_globalCts.IsCancellationRequested)
                return EntityExecutionStatus.Ongoing;

            // Scheduled (not terminal)
            if (commandsStatuses.Any(status => status == CommandExecutionStatus.Scheduled) &&
                !commandsStatuses.Any(status => status == CommandExecutionStatus.Ongoing) &&
                !_globalCts.IsCancellationRequested)
                return EntityExecutionStatus.Scheduled;

            // NotStarted (not terminal)
            if (!commandsStatuses.Any() && !_globalCts.IsCancellationRequested)
                return EntityExecutionStatus.NotStarted;

            // Cancelled (not in terminal-cache list by requirement)
            if (_globalCts.IsCancellationRequested)
                return CacheAndReturn(httpIteration, EntityExecutionStatus.Cancelled);

            // Success -> terminal
            return CacheAndReturn(httpIteration, EntityExecutionStatus.Success);
        }

        public async Task<bool> IsTerminatedAsync(HttpIteration httpIteration, CancellationToken token = default)
        {
            // If cached terminal exists and it is Terminated, short-circuit.
            if (TryGetCachedTerminal(httpIteration, out var cached))
                return cached == EntityExecutionStatus.Terminated;

            return await _terminationChecker.IsTerminationRequiredAsync(httpIteration, token);
        }

        public async Task<bool> IsFailedAsync(HttpIteration httpIteration, CancellationToken token = default)
        {
            // If cached terminal exists and it is Failed, short-circuit.
            if (TryGetCachedTerminal(httpIteration, out var cached))
                return cached == EntityExecutionStatus.Failed;

            return await _iterationFailureEvaluator.IsErrorRateExceededAsync(httpIteration, token);
        }

        public bool IsCancelled(HttpIteration httpIteration)
        {
            // Cancelled is not considered a terminal-cache status.
            return _globalCts.IsCancellationRequested;
        }

        // ---------- Helpers ----------

        private static bool IsTerminalStatus(EntityExecutionStatus status) =>
            status == EntityExecutionStatus.Skipped ||
            status == EntityExecutionStatus.Failed ||
            status == EntityExecutionStatus.Success ||
            status == EntityExecutionStatus.Cancelled ||
            status == EntityExecutionStatus.Terminated;

        private static bool TryGetCachedTerminal(HttpIteration httpIteration, out EntityExecutionStatus status)
        {
            if (httpIteration == null) throw new ArgumentNullException(nameof(httpIteration));
            return _terminalStatusCache.TryGetValue(httpIteration.Id, out status);
        }

        private static EntityExecutionStatus CacheAndReturn(HttpIteration httpIteration, EntityExecutionStatus status)
        {
            if (IsTerminalStatus(status))
            {
                // Upsert the terminal status for this iteration id
                _terminalStatusCache[httpIteration.Id] = status;
            }
            return status;
        }
    }
}
