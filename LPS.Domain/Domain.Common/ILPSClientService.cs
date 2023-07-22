using System;
using System.Threading;
using System.Threading.Tasks;

namespace LPS.Domain.Common
{
    public interface ILPSClientService<TRequest> where TRequest : IRequestable
    {
        public string Id { get; }
        public string GuidId { get; }

        async Task Send(TRequest request, string requestId, CancellationToken cancellationToken) => await Task.CompletedTask; // default implementation
    }
}