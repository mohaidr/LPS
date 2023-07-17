using System;
using System.Threading;
using System.Threading.Tasks;

namespace LPS.Domain.Common
{
    public interface ILPSClientService<TRequest> where TRequest : IRequestable
    {
        public int Id { get; }
        async Task Send(TRequest request, string requestId, CancellationToken cancellationToken) => await Task.CompletedTask; // default implementation
    }
}