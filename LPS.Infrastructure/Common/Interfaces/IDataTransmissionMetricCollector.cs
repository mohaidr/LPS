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
        public void UpdateDataSentAsync(double dataSize, CancellationToken token);

        public void UpdateDataReceivedAsync(double dataSize, CancellationToken token);
    }
}
