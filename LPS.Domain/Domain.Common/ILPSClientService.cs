using System;
using System.Threading;
using System.Threading.Tasks;

namespace LPS.Domain.Common
{
    public interface ILPSClientService<TRequest> where TRequest : ILPSRequestEntity
    {
        public string Id { get; }
        public string GuidId { get; }

        Task SendAsync(TRequest request, string requestId, CancellationToken cancellationToken);
    }
}