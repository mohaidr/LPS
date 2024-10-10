using LPS.Domain.Common.Interfaces;
using System;
using System.Collections.Generic;

namespace LPS.Domain.Domain.Common.Interfaces
{
    public interface ICommandStatusMonitor<TCommand, TEntity> where TCommand : IAsyncCommand<TEntity> where TEntity : IDomainEntity
    {
        public void RegisterCommand(TCommand command, TEntity entity);
        public void UnRegisterCommand(TCommand command, TEntity entity);
        public bool IsAnyCommandOngoing(TEntity entity);
        List<CommandExecutionStatus> GetAllStatuses(TEntity entity);
    }
}