using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using LPS.Domain.Common.Interfaces;
using LPS.Domain;
using LPS.Infrastructure.Monitoring.Metrics;
using System;
using LPS.Infrastructure.Logger;

namespace LPS.Infrastructure.Common.Interfaces
{
    /// <summary>
    /// Provides methods for tracking metrics such as connection counts, data transfer, and response times.
    /// </summary>
    public interface IMetricsService
    {
        /// <summary>
        /// Attempts to increment the active connections count for the specified request.
        /// </summary>
        ValueTask<bool> TryIncreaseConnectionsCountAsync(Guid requestId, CancellationToken token);

        /// <summary>
        /// Attempts to decrement the active connections count for the specified request, 
        /// marking it as successful or not.
        /// </summary>
        ValueTask<bool> TryDecreaseConnectionsCountAsync(Guid requestId, CancellationToken token);

        /// <summary>
        /// Attempts to update response-related metrics for the specified request.
        /// </summary>
        ValueTask<bool> TryUpdateResponseMetricsAsync(Guid requestId, HttpResponse.SetupCommand response, CancellationToken token);

        /// <summary>
        /// Attempts to update the metrics for data sent for the specified request.
        /// </summary>
        /// <param name="requestId">The identifier of the request.</param>
        /// <param name="totalBytes">The total number of bytes sent.</param>
        /// <param name="elapsedTicks">
        /// The elapsed time, in ticks, taken to upload the total bytes.
        /// This represents the duration of the data transmission.
        /// </param>
        ValueTask<bool> TryUpdateDataSentAsync(Guid requestId, double totalBytes, CancellationToken token);

        /// <summary>
        /// Attempts to update the metrics for data received for the specified request.
        /// </summary>
        /// <param name="requestId">The identifier of the request.</param>
        /// <param name="totalBytes">The total number of bytes received.</param>
        /// <param name="elapsedTicks">
        /// The elapsed time, in ticks, taken to download the total bytes.
        /// This represents the duration of the data reception.
        /// </param>
        ValueTask<bool> TryUpdateDataReceivedAsync(Guid requestId, double totalBytes, CancellationToken token);
    }

}
