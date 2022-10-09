using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AsyncTest.Domain.Common
{
    public interface ICommand<TEntity> where TEntity: IExecutable
    {
        void Execute(TEntity entity);
        Task ExecuteAsync(TEntity entity);
    }
}
