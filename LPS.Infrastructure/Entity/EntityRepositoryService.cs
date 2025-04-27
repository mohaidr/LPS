#nullable enable

using LPS.Domain.Common.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LPS.Infrastructure.Entity
{
    public class EntityRepositoryService : IEntityRepositoryService
    {
        private readonly Dictionary<Type, object> _store = new();

        public void Add<T>(T entity) where T : IDomainEntity
        {
            var type = typeof(T);
            if (!_store.TryGetValue(type, out var collection))
            {
                collection = new EntityCollection<T>(); // You can later use DI to inject a different implementation
                _store[type] = collection;
            }

            ((IEntityCollection<T>)collection).Add(entity);
        }

        public T? Get<T>(Guid id) where T : IDomainEntity
        {
            return _store.TryGetValue(typeof(T), out var collection)
                ? ((IEntityCollection<T>)collection).Get(id)
                : default;
        }

        public bool Remove<T>(Guid id) where T : IDomainEntity
        {
            return _store.TryGetValue(typeof(T), out var collection)
                && ((IEntityCollection<T>)collection).Remove(id);
        }

        public IEnumerable<T> Query<T>(Func<T, bool>? predicate = null) where T : IDomainEntity
        {
            if (_store.TryGetValue(typeof(T), out var collection))
            {
                var items = ((IEntityCollection<T>)collection).All();
                return predicate != null ? items.Where(predicate) : items;
            }

            return Enumerable.Empty<T>();
        }
    }
}
