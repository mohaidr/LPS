using LPS.Domain.Common.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LPS.Infrastructure.Entity
{
    public interface IEntityRepositoryService
    {
        public void Add<T>(T entity) where T : IDomainEntity;
        public T? Get<T>(Guid id) where T : IDomainEntity;
        public bool Remove<T>(Guid id) where T : IDomainEntity;
        public IEnumerable<T> Query<T>(Func<T, bool>? predicate = null) where T : IDomainEntity;
    }
}
