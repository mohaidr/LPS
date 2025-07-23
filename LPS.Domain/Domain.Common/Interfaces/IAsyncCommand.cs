using LPS.Domain.Domain.Common.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace LPS.Domain.Common.Interfaces
{

    public interface IAsyncCommand<TEntity> where TEntity : IDomainEntity
    {
        public CommandExecutionStatus Status { get; }
        public Task ExecuteAsync(TEntity entity, CancellationToken token);
    }
}
