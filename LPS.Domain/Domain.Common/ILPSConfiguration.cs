using System;

namespace LPS.Domain.Common
{
    public interface ILPSClientConfiguration<T> where T : ILPSRequestEntity
    {
    }

    public interface ILPSHttpClientConfiguration<T>: ILPSClientConfiguration<T> where T : ILPSRequestEntity
    {
        public TimeSpan PooledConnectionLifetime { get; set; }
        public TimeSpan PooledConnectionIdleTimeout { get; set; }
        public int MaxConnectionsPerServer { get; set; }
        public TimeSpan Timeout { get; set; }
    }
}