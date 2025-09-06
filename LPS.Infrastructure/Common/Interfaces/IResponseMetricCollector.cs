using LPS.Domain;
using System.Threading;
using System.Threading.Tasks;

namespace LPS.Infrastructure.Common.Interfaces
{
    public interface IResponseMetricCollector : IMetricAggregator
    {
        public IResponseMetricCollector Update(HttpResponse.SetupCommand httpResponse, CancellationToken token);
        public Task<IResponseMetricCollector> UpdateAsync(HttpResponse.SetupCommand httpResponse, CancellationToken token);
    }
}
