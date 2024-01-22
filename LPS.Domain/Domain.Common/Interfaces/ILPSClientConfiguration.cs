using System;

namespace LPS.Domain.Common.Interfaces
{
    public interface ILPSClientConfiguration<T> where T : ILPSRequestEntity
    {
    }

    public interface ILPSHttpClientConfiguration<T>: ILPSClientConfiguration<T> where T : ILPSRequestEntity
    {
        public TimeSpan PooledConnectionLifetime { get; }
        public TimeSpan PooledConnectionIdleTimeout { get; }
        public int MaxConnectionsPerServer { get; }
        public TimeSpan Timeout { get; }
    }
}