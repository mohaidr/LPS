using LPS.Domain;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LPS.Infrastructure.Common.Interfaces
{
    public interface IResponseMetric : IMetricMonitor
    {
        public IResponseMetric Update(HttpResponse httpResponse);
        public Task<IResponseMetric> UpdateAsync(HttpResponse httpResponse);
    }
}
