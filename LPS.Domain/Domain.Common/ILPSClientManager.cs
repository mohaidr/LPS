using System;

namespace LPS.Domain.Common
{
    public interface ILPSClientManager<T1, T2> where T1 : IRequestable where T2: ILPSClientService<T1>
    {
        T2 CreateInstance(ILPSClientConfiguration<T1> config);

        public void CreateAndQueueClient(ILPSClientConfiguration<T1> config);

        public ILPSClientService<T1> DequeueClient();

        public ILPSClientService<T1> DequeueClient(ILPSClientConfiguration<T1> config, bool byPassQueueIfEmpty);


    }

    public interface ILPSHttpClientManager<T1, T2>: ILPSClientManager<T1, T2> where T1 : IRequestable where T2 : ILPSClientService<T1>
    {
       
    }
}