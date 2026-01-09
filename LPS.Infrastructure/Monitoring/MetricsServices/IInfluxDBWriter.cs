#nullable enable
using System.Threading.Tasks;
using LPS.Infrastructure.Monitoring.Cumulative;
using LPS.Infrastructure.Monitoring.Windowed;

namespace LPS.Infrastructure.Monitoring.MetricsServices
{
    /// <summary>
    /// Uploads metrics snapshots to customer's InfluxDB instance.
    /// </summary>
    public interface IInfluxDBWriter
    {
        /// <summary>
        /// Uploads windowed metrics snapshot to InfluxDB.
        /// Non-blocking fire-and-forget operation.
        /// </summary>
        Task UploadWindowedMetricsAsync(WindowedIterationSnapshot snapshot);

        /// <summary>
        /// Uploads cumulative metrics snapshot to InfluxDB.
        /// Non-blocking fire-and-forget operation.
        /// </summary>
        Task UploadCumulativeMetricsAsync(CumulativeIterationSnapshot snapshot);
    }
}
