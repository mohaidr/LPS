using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LPS.UI.Common.DTOs
{
    public interface IDto<T> where T : IDto<T>
    {
        void DeepCopy(out T dto);
    }

    public abstract class DtoBase<T> : IDto<T>
        where T : DtoBase<T>, IDto<T>
    {
        public Guid Id { get; set; }
        // Shallow copy by default; override for true deep copy of nested refs.
        public virtual void DeepCopy(out T dto) => dto = (T)MemberwiseClone();
    }

}
