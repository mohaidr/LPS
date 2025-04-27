using LPS.Domain.Common.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LPS.Infrastructure.Entity
{
    public class EntityCollection<T> : IEntityCollection<T> where T : IDomainEntity
    {
        private readonly Dictionary<Guid, T> _entities = new();

        public void Add(T entity)
        {
            _entities[entity.Id] = entity;
        }

        public T? Get(Guid id) => _entities.TryGetValue(id, out var entity) ? entity : default;

        public bool Remove(Guid id) => _entities.Remove(id);

        public IEnumerable<T> All() => _entities.Values;
    }

}
