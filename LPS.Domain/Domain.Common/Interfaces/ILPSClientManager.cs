using System;

namespace LPS.Domain.Common.Interfaces
{
    public interface ILPSClientManager<T1, T2, T3> where T1 : ILPSRequestEntity where T2 : ILPSResponseEntity where T3: ILPSClientService<T1, T2>
    {
        T3 CreateInstance(ILPSClientConfiguration<T1> config);

        public void CreateAndQueueClient(ILPSClientConfiguration<T1> config);

        public ILPSClientService<T1, T2> DequeueClient();

        public ILPSClientService<T1, T2> DequeueClient(ILPSClientConfiguration<T1> config, bool byPassQueueIfEmpty);


    }

    public interface ILPSHttpClientManager<T1, T2, T3> : ILPSClientManager<T1, T2, T3> where T1 : ILPSRequestEntity where T2 : ILPSResponseEntity where T3 : ILPSClientService<T1, T2>
    {
       
    }
}