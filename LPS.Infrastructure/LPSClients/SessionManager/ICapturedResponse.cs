using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LPS.Infrastructure.LPSClients.SessionManager
{
    public interface ICapturedResponse
    {
        public string RawResponse { get;}
        public string Format { get; }
        public string ExtractJsonValue(string pattern);
        public string ExtractRegexMatch(string pattern);
        public string ExtractXmlValue(string xpath);
    }
}
