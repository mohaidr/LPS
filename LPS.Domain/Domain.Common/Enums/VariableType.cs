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
        QString,
        // Dealing with it as a special string type which you can navigate through with path (.,/,[])
        JsonString,
        XmlString,
        CsvString,

        QJsonString,
        QXmlString,
        QCsvString,
        //Number Variable Type
        Float,
        Double,
        Int,
        Decimal,

        //Boolean Variable Type
        Boolean,

        // HttpResponse (Special Type)
        HttpResponse,

        KeyValue,

        Multiple,

        Wrapper
    }

}
