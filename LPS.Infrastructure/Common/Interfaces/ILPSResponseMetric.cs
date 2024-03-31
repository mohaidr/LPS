using LPS.Domain;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LPS.Infrastructure.Common.Interfaces
{
    public interface ILPSResponseMetric : ILPSMetric
    {
        public ILPSResponseMetric Update(LPSHttpResponse httpResponse);
        public Task<ILPSResponseMetric> UpdateAsync(LPSHttpResponse httpResponse);
    }
}
