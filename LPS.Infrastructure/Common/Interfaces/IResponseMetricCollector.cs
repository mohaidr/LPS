using LPS.Domain;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LPS.Infrastructure.Common.Interfaces
{
    public interface IResponseMetricCollector : IMetricCollector
    {
        public IResponseMetricCollector Update(HttpResponse httpResponse);
        public Task<IResponseMetricCollector> UpdateAsync(HttpResponse httpResponse);
    }
}
