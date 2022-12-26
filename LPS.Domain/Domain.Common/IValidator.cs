using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LPS.Domain.Common
{
    public interface IValidator<TEntity, TCommand> where TEntity: IExecutable where TCommand: ICommand<TEntity>
    {
       void Validate(TEntity entity, TCommand dto);
    }
}
