using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LPS.DTOs
{
    public class VariableDto
    {
        public VariableDto()
        {
            As = "Text";
            Regex = string.Empty;
        }
        public string Name { get; set; }
        public string Value { get; set; }
        public string As { get; set; }
        public string Regex { get; set; }
    }
}
