using System;
using System.Threading;
using System.Threading.Tasks;

namespace LPS.Domain.Common.Interfaces
{
    public interface ILPSClientService<TRequest, TResponse> where TRequest : ILPSRequestEntity where TResponse : ILPSResponseEntity
    {
        public string Id { get; }
        public string GuidId { get; }

        Task<TResponse> SendAsync(TRequest request, ICancellationTokenWrapper cancellationTokenWrapper);
    }
}