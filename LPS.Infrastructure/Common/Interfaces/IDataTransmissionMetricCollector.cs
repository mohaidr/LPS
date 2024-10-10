using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace LPS.Infrastructure.Common.Interfaces
{
    public interface IDataTransmissionMetricCollector
    {
        public Task UpdateDataSentAsync(double dataSize, CancellationToken token);

        public Task UpdateDataReceivedAsync(double dataSize, CancellationToken token);
        // Add more logic as needed for monitoring data sent/received
    }
}
