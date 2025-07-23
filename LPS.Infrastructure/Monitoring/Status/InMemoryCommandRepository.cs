using LPS.Domain.Common.Interfaces;
using LPS.Domain.Domain.Common.Interfaces;
using LPS.Domain;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LPS.Infrastructure.Monitoring.Status
{
    public class InMemoryCommandRepository<TCommand, TEntity> : ICommandRepository<TCommand, TEntity>
        where TCommand : IAsyncCommand<TEntity>
        where TEntity : HttpIteration
    {
        private readonly ConcurrentDictionary<TEntity, ConcurrentBag<TCommand>> _registry = new();

        public void Register(TCommand command, TEntity entity)
        {
            var commands = _registry.GetOrAdd(entity, _ => new ConcurrentBag<TCommand>());
            commands.Add(command);
        }

        public void UnRegister(TCommand command, TEntity entity)
        {
            if (_registry.TryGetValue(entity, out var commands))
            {
                var filtered = new ConcurrentBag<TCommand>(commands.Where(c => !c.Equals(command)));
                if (filtered.IsEmpty)
                    _registry.TryRemove(entity, out _);
                else
                    _registry[entity] = filtered;
            }
        }

        public IEnumerable<TCommand> GetCommands(TEntity entity)
        {
            return _registry.TryGetValue(entity, out var commands) ? commands : Enumerable.Empty<TCommand>();
        }

        public IEnumerable<TEntity> GetEntities(Func<TEntity, bool> predicate)
        {
            return _registry.Keys.Where(predicate);
        }
    }

}
