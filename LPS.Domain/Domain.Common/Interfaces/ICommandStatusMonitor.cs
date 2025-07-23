using LPS.Domain.Common.Interfaces;
using LPS.Domain.Domain.Common.Enums;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace LPS.Domain.Domain.Common.Interfaces
{
    /// <summary>
    /// Monitors and tracks the execution status of commands associated with HttpIteration entities.
    /// </summary>
    public interface ICommandStatusMonitor<TEntity> where TEntity : IDomainEntity
    {
        /// <summary>
        /// Checks whether any command associated with the given entity is currently ongoing.
        /// </summary>
        /// <param name="entity">The entity to check.</param>
        /// <returns>True if any command is in an ongoing state; otherwise, false.</returns>
        public ValueTask<bool> IsAnyCommandOngoing(TEntity entity);
        /// <summary>
        /// Retrieves the list of execution statuses for all commands associated with the given entity.
        /// </summary>
        /// <param name="entity">The entity whose command statuses are to be queried.</param>
        /// <returns>A list of execution statuses for the associated commands.</returns>
        ValueTask<List<CommandExecutionStatus>> QueryAsync(TEntity entity);

        /// <summary>
        /// Retrieves execution statuses for all entities matching the specified predicate.
        /// </summary>
        /// <param name="predicate">A filter to select relevant entities from the registry.</param>
        /// <returns>
        /// A dictionary mapping each matching entity to a list of execution statuses 
        /// for its associated commands.
        /// </returns>
        public ValueTask<Dictionary<TEntity, IList<CommandExecutionStatus>>> QueryAsync(Func<TEntity, bool> predicate);
    }
}