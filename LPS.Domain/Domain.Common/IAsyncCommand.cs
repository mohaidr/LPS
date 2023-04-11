using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace LPS.Domain.Common
{
    public interface IAsyncCommand<TEntity> where TEntity: IExecutable
    {
        Task ExecuteAsync(TEntity entity, CancellationToken cancellationToken);
    }
}
