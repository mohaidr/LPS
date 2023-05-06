using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LPS.Domain.Common
{
    public interface IValidCommand
    {
        bool IsValid { get; }
        IDictionary<string, string> ValidationErrors { get; set; }
    }
}
