using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LPS.DTOs
{
    //TODO Create Domain entities for those so we can store them in DB once implemented.
    public class VariableDto
    {
        public VariableDto()
        {
            As = string.Empty;
            Regex = string.Empty;
        }
        public string Name { get; set; }
        public string Value { get; set; }
        public string As { get; set; }
        public string Regex { get; set; }
    }
}
