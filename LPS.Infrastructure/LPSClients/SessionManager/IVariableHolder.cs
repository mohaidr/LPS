using LPS.Domain.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LPS.Infrastructure.LPSClients.SessionManager
{
    public interface IVariableHolder
    {
        public static bool IsKnownSupportedFormat(MimeType mimeType)
        {
            return mimeType == MimeType.ApplicationJson ||
                   mimeType == MimeType.RawXml ||
                   mimeType == MimeType.TextXml ||
                   mimeType == MimeType.ApplicationXml ||
                   mimeType == MimeType.TextPlain ||
                   mimeType == MimeType.TextCsv;
        }
        public string Value { get;}
        public MimeType Format { get; }
        public string Pattern { get; }
        public bool IsGlobal { get; }
        public string ExtractJsonValue(string pattern);
        public string ExtractXmlValue(string xpath);
        public string ExtractCsvValue(string indices);
        public string ExtractValueWithRegex();
    }
}
