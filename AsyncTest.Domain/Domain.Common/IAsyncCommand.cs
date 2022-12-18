using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AsyncTest.Domain.Common
{
    public interface IAsyncCommand<TEntity> where TEntity: IExecutable
    {
        Task ExecuteAsync(TEntity entity);
    }
}
