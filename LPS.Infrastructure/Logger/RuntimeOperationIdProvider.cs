using LPS.Domain.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LPS.Infrastructure.Logger
{
    public class RuntimeOperationIdProvider : IRuntimeOperationIdProvider
    {
        private static readonly Lazy<Guid> operationId = new Lazy<Guid>(() => Guid.NewGuid());

        public string OperationId => operationId.Value.ToString();
    }
}
