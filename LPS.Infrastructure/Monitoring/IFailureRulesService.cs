#nullable enable
using System.Collections.Generic;
using LPS.Domain;
using LPS.Domain.Domain.Common.Enums;

namespace LPS.Infrastructure.Monitoring
{
    /// <summary>
    /// Service for evaluating failure rules and determining error status codes.
    /// Provides caching to avoid repeated parsing of failure rules.
    /// </summary>
    public interface IFailureRulesService
    {
        /// <summary>
        /// Gets the error status code filters for a given iteration.
        /// Results are cached per iteration ID and round name.
        /// </summary>
        /// <param name="iteration">The HTTP iteration containing failure rules</param>
        /// <param name="roundName">The round name for cache key uniqueness</param>
        /// <returns>List of error status code filters (operator, threshold, max threshold)</returns>
        IReadOnlyList<(ComparisonOperator Op, double Threshold, double? ThresholdMax)> GetErrorStatusCodeFilters(
            HttpIteration iteration, 
            string roundName);

        /// <summary>
        /// Checks if a given status code is considered an error based on the iteration's failure rules.
        /// </summary>
        /// <param name="iteration">The HTTP iteration containing failure rules</param>
        /// <param name="roundName">The round name for cache key uniqueness</param>
        /// <param name="statusCode">The HTTP status code to evaluate</param>
        /// <returns>True if the status code matches any error filter, false otherwise</returns>
        bool IsErrorStatusCode(HttpIteration iteration, string roundName, int statusCode);
    }
}
