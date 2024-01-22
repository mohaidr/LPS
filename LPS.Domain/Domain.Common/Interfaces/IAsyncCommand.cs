using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace LPS.Domain.Common.Interfaces
{
    public interface IAsyncCommand<TEntity> where TEntity: IDomainEntity
    {
        Task ExecuteAsync(TEntity entity, ICancellationTokenWrapper cancellationTokenWrapper);
    }
}
