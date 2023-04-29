using System;

namespace LPS.Domain.Common
{
    public interface ILPSClientManager<T1, T2> where T1 : IRequestable where T2: ILPSClientService<T1>
    {
        T2 CreateInstance(ILPSClientConfiguration<LPSHttpRequest> config);
    }

    public interface ILPSHttpClientManager<T1, T2>: ILPSClientManager<T1, T2> where T1 : IRequestable where T2 : ILPSClientService<T1>
    {
       
    }
}