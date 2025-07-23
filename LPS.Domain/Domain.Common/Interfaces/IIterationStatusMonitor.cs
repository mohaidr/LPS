using LPS.Domain.Domain.Common.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace LPS.Domain.Domain.Common.Interfaces
{
    public interface IIterationStatusMonitor
    {
        /// <summary>
        /// Gets the terminal execution status for a given HTTP iteration.
        /// </summary>
        /// <param name="httpIteration">The HTTP iteration to evaluate.</param>
        /// <returns>The final execution status.</returns>
        ValueTask<EntityExecutionStatus> GetTerminalStatusAsync(HttpIteration httpIteration, CancellationToken token = default);

        /// <summary>
        /// Determines whether the iteration is terminated.
        /// </summary>
        /// <param name="httpIteration">The HTTP iteration to evaluate.</param>
        /// <returns>True if terminated, false otherwise.</returns>
        Task<bool> IsTerminatedAsync(HttpIteration httpIteration, CancellationToken token = default);

        /// <summary>
        /// Determines whether the iteration has failed due to error rate.
        /// </summary>
        /// <param name="httpIteration">The HTTP iteration to evaluate.</param>
        /// <returns>True if failed, false otherwise.</returns>
        Task<bool> IsFailedAsync(HttpIteration httpIteration, CancellationToken token = default);

        /// <summary>
        /// Checks if the operation was cancelled.
        /// </summary>
        /// <param name="httpIteration">The HTTP iteration (for context, if needed).</param>
        /// <returns>True if cancelled, false otherwise.</returns>
        bool IsCancelled(HttpIteration httpIteration);
    }

}
