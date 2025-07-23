using LPS.Domain.Common.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LPS.Domain.Domain.Common.Interfaces
{
    public interface ICommandRepository<TCommand, TEntity>
        where TCommand : IAsyncCommand<TEntity>
        where TEntity : HttpIteration
    {
        /// <summary>
        /// Registers a command for the specified entity, tracking its execution status.
        /// </summary>
        /// <param name="command">The command to register.</param>
        /// <param name="entity">The entity associated with the command.</param>
        public void Register(TCommand command, TEntity entity);
        /// <summary>
        /// Unregisters a command from the specified entity, removing it from tracking.
        /// </summary>
        /// <param name="command">The command to unregister.</param>
        /// <param name="entity">The entity associated with the command.</param>
        public void UnRegister(TCommand command, TEntity entity);

        IEnumerable<TCommand> GetCommands(TEntity entity);
        IEnumerable<TEntity> GetEntities(Func<TEntity, bool> predicate);
    }
}
