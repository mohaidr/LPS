using System;

namespace LPS.Infrastructure.Common.Interfaces
{
    /// <summary>
    /// Provides plan-level execution context for the current test run.
    /// Used to propagate plan name and test start time to metrics snapshots.
    /// </summary>
    public interface IPlanExecutionContext
    {
        /// <summary>
        /// Name of the currently executing plan.
        /// </summary>
        string PlanName { get; }

        /// <summary>
        /// UTC timestamp when the test execution started.
        /// </summary>
        DateTime TestStartTime { get; }

        /// <summary>
        /// Sets the plan execution context. Called once at test start.
        /// </summary>
        void SetContext(string planName, DateTime testStartTime);

        /// <summary>
        /// Clears the context. Called after test completes.
        /// </summary>
        void Clear();
    }
}
