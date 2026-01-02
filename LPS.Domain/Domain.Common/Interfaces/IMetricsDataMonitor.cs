using System.Threading.Tasks;

namespace LPS.Domain.Common.Interfaces
{
    /// <summary>
    /// Registers iterations for metrics collection.
    /// Collectors self-manage their lifecycle via IIterationStatusMonitor.
    /// </summary>
    public interface IMetricsDataMonitor
    {
        /// <summary>
        /// Registers an iteration for metrics collection. 
        /// The collectors are created and automatically start listening to coordinator events.
        /// </summary>
        ValueTask<bool> TryRegisterAsync(string roundName, HttpIteration lpsHttpIteration);
    }
}
