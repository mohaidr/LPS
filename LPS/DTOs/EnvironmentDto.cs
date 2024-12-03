using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LPS.DTOs
{
    public class EnvironmentDto
    {
        public EnvironmentDto()
        {
            Name = string.Empty;
            Variables = [];
        }
        public string Name { get; set; }
        public IList<VariableDto> Variables { get; set; }
    }
}
