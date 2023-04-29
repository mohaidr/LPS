using System;
using System.Threading;
using System.Threading.Tasks;

namespace LPS.Domain.Common
{
    public interface ILPSClientService<T> where T : IRequestable
    {
        async Task Send(T t, string requestId, CancellationToken cancellationToken) => await Task.CompletedTask; // default implementation
    }
}