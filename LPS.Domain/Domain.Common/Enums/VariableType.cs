using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LPS.Domain.Domain.Common.Enums
{
    public enum VariableType
    {
        // String Variable Type
        String,

        // Dealing with it as a special string type which you can navigate through with path (.,/,[])
        JsonString,
        XmlString,
        CsvString,

        //Number Variable Type
        Float,
        Double,
        Int,
        Decimal,

        //Boolean Variable Type
        Boolean,

        // HttpResponse (Special Type)
        HttpResponse
    }

}
