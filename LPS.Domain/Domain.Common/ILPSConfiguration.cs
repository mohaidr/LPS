using System;

namespace LPS.Domain.Common
{
    public interface ILPSClientConfiguration<T> where T : IRequestable
    {
    }

    public interface ILPSHttpClientConfiguration<T>: ILPSClientConfiguration<T> where T : IRequestable
    {
        public TimeSpan PooledConnectionLifetime { get; set; }
        public TimeSpan PooledConnectionIdleTimeout { get; set; }
        public int MaxConnectionsPerServer { get; set; }
        public TimeSpan Timeout { get; set; }
    }
}