using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AsyncTest.Domain.Common
{
    interface IValidEntity
    {
        bool  IsValid  { get; }
    }
}
