using LPS.Domain;
using LPS.Domain.Domain.Common.Enums;
using LPS.Domain.Domain.Common.Interfaces;
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

        public async ValueTask<EntityExecutionStatus> GetTerminalStatusAsync(HttpIteration httpIteration, CancellationToken token = default) 
        {
            
            if (_globalCts.IsCancellationRequested)
            {
                return EntityExecutionStatus.Cancelled;
            }
            if (await _terminationChecker.IsTerminationRequiredAsync(httpIteration, token))
            {
                return EntityExecutionStatus.Terminated;
            }
            if(await _iterationFailureEvaluator.IsErrorRateExceededAsync(httpIteration, token))
            {
                return EntityExecutionStatus.Failed;
            }
            if (await _commandStatusMonitor.IsAnyCommandOngoing(httpIteration))
            {
                return EntityExecutionStatus.Ongoing;
            }
            if ((await _commandStatusMonitor.QueryAsync(httpIteration)).Any(status => status == CommandExecutionStatus.Scheduled))
            {
                return EntityExecutionStatus.Scheduled;
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
