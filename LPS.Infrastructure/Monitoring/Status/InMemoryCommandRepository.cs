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
    public class InMemoryCommandRepository<TEntity, TCommand> : ICommandRepository<TEntity, TCommand>
        where TCommand : IAsyncCommand<TEntity>
        where TEntity : HttpIteration
    {
        private readonly ConcurrentDictionary<TEntity, ConcurrentBag<TCommand>> _registry = new();

        public void Add(TEntity key,TCommand value)
        {
            var commands = _registry.GetOrAdd(key, _ => new ConcurrentBag<TCommand>());
            commands.Add(value);
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
