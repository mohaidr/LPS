using LPS.Domain;
using LPS.Domain.Domain.Common.Enums;
using LPS.Domain.Domain.Common.Interfaces;
using LPS.Protos.Shared;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace LPS.Infrastructure.Monitoring.Status
{
    // The last update we need here, is if this is a worker, then we should call the service on the master
    public class IterationStatusMonitor: IIterationStatusMonitor
    {
        readonly ITerminationCheckerService _terminationChecker;
        readonly IIterationFailureEvaluator _iterationFailureEvaluator;
        readonly CancellationTokenSource _globalCts;
        readonly ICommandStatusMonitor<HttpIteration> _commandStatusMonitor;

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

        // This has to be recoded, there is the case where a client (let us call it client 2) would starts an iteration after a client (let us name it 1) finish the execution of the same iteration then the statuses in the repo would not reflect the actual state as the command of client 2 is not registered yet.
        public async ValueTask<EntityExecutionStatus> GetTerminalStatusAsync(HttpIteration httpIteration, CancellationToken token = default) 
        {
            
            if (await _terminationChecker.IsTerminationRequiredAsync(httpIteration, token))
            {
                return EntityExecutionStatus.Terminated;
            }
            if(await _iterationFailureEvaluator.IsErrorRateExceededAsync(httpIteration, token))
            {
                return EntityExecutionStatus.Failed;
            }

            var commandsStatuses  = await _commandStatusMonitor.QueryAsync(httpIteration);

            if (commandsStatuses.Count != 0 && commandsStatuses.All(status => status == CommandExecutionStatus.Skipped))
            {
                return EntityExecutionStatus.Skipped;
            }
            if (commandsStatuses.Any(status => status == CommandExecutionStatus.Skipped) && !commandsStatuses.All(status => status == CommandExecutionStatus.Skipped))
            {
                return EntityExecutionStatus.PartiallySkipped;
            }

            if (commandsStatuses.Any(status => status == CommandExecutionStatus.Ongoing) && !_globalCts.IsCancellationRequested)
            {
                return EntityExecutionStatus.Ongoing;
            }
            if (commandsStatuses.Any(status => status == CommandExecutionStatus.Scheduled) && !_globalCts.IsCancellationRequested)
            {
                return EntityExecutionStatus.Scheduled;
            }

            if (!commandsStatuses.Any() && !_globalCts.IsCancellationRequested)
            {
                return EntityExecutionStatus.NotStarted;
            }
            if (_globalCts.IsCancellationRequested)
            {
                return EntityExecutionStatus.Cancelled;
            }
            return EntityExecutionStatus.Success;
        }

        public async Task<bool> IsTerminatedAsync(HttpIteration httpIteration, CancellationToken token = default)
        {
            return await _terminationChecker.IsTerminationRequiredAsync(httpIteration, token);
        }

        public async Task<bool> IsFailedAsync(HttpIteration httpIteration, CancellationToken token = default)
        {
           return await _iterationFailureEvaluator.IsErrorRateExceededAsync(httpIteration, token);
        }

        public bool IsCancelled(HttpIteration httpIteration)
        {
            return _globalCts.IsCancellationRequested;
        }
    }
}
