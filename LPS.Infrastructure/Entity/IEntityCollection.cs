using LPS.Domain.Common.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LPS.Infrastructure.Entity
{
    public interface IEntityCollection<T> where T : IDomainEntity
    {
        void Add(T entity);
        T? Get(Guid id);
        bool Remove(Guid id);
        IEnumerable<T> All();  // Add this!
    }


}
