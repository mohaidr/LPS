using AsyncTest.Domain.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AsyncTest.Domain.Common
{

    public interface IFileLogger : ICustomLogger
    {
        public Task Flush();
    }
}
