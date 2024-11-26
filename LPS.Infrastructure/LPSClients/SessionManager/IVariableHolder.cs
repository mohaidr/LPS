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
        public bool CheckIfSupportsParsing(MimeType mimeType);
        public string RawValue { get;}
        public MimeType Format { get; }
        public string Pattern { get; }
        public bool IsGlobal { get; }
        public string ExtractJsonValue(string pattern);
        public string ExtractXmlValue(string xpath);
        public string ExtractValueWithRegex();
    }
}
