using LPS.Domain.Domain.Common.Enums;
using LPS.Infrastructure.VariableServices.VariableHolders;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LPS.Infrastructure.VariableServices
{
    public static class VariableFormatExtensions
    {
        public static bool IsNumeric(this VariableType format) =>
            format == VariableType.Int || format == VariableType.Float || format == VariableType.Double;

        public static bool IsStructuredText(this VariableType format) =>
            format == VariableType.JsonString || format == VariableType.XmlString || format == VariableType.CsvString;

    }

}
