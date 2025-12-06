using System.Threading;
using System.Threading.Tasks;

namespace LPS.Infrastructure.Monitoring
{
    /// <summary>
    /// Interface for fetching metric values from the gRPC metrics service.
    /// Provides a unified way to retrieve current metric values for evaluation in
    /// failure rules, termination rules, and success rules.
    /// </summary>
    public interface IMetricFetcher
    {
        /// <summary>
        /// Gets the current value of a metric by name.
        /// </summary>
        /// <param name="fqdn">Fully qualified domain name of the iteration</param>
        /// <param name="metricName">Name of the metric (e.g., "ErrorRate", "TotalTime.P90", "TTFB.Average")</param>
        /// <param name="token">Cancellation token</param>
        /// <returns>Current value of the metric</returns>
        /// <exception cref="System.ArgumentException">Thrown when metric name is unknown</exception>
        Task<double> GetMetricValueAsync(string fqdn, string metricName, CancellationToken token);

        /// <summary>
        /// Gets the current value of a metric by name with additional context.
        /// For ErrorRate metrics, this allows specifying which status codes count as errors.
        /// </summary>
        /// <param name="fqdn">Fully qualified domain name of the iteration</param>
        /// <param name="metricName">Name of the metric (e.g., "ErrorRate", "TotalTime.P90")</param>
        /// <param name="errorStatusCodes">For ErrorRate: status code filter expression (e.g., ">= 500", "between 400 and 599"). 
        /// If null, defaults to ">= 400".</param>
        /// <param name="token">Cancellation token</param>
        /// <returns>Current value of the metric</returns>
        Task<double> GetMetricValueAsync(string fqdn, string metricName, string errorStatusCodes, CancellationToken token);
    }
}
