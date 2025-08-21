using LPS.Domain.Common.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LPS.Domain.Domain.Common.Interfaces
{
    public interface ICommandRepository<TEntity, TCommand>
        where TCommand : IAsyncCommand<TEntity>
        where TEntity : HttpIteration
    {
        /// <summary>
        /// Registers a command for the specified entity, tracking its execution status.
        /// </summary>
        /// <param name="value">The command to register.</param>
        /// <param name="key">The entity associated with the command.</param>
        public void Add(TEntity key, TCommand value);
        IEnumerable<TCommand> GetCommands(TEntity entity);
        IEnumerable<TEntity> GetEntities(Func<TEntity, bool> predicate);
    }
}
