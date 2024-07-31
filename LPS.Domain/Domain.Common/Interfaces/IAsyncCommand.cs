using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace LPS.Domain.Common.Interfaces
{
    public enum AsyncCommandStatus
    {
        NotStarted,
        Ongoing,
        Paused,
        Cancelled,
        Completed,
        Failed
    }
    public interface IAsyncCommand<TEntity> where TEntity : IDomainEntity
    {
        public AsyncCommandStatus Status { get; }
        Task ExecuteAsync(TEntity entity);
    }
}
