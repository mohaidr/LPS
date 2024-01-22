using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LPS.Domain.Common.Interfaces
{
    public interface ILPSRuntimeOperationIdProvider
    {
        public string OperationId { get; }
    }
}
