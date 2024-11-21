using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace LPS.Infrastructure.LPSClients.HeaderServices
{
    public interface IHttpHeadersService
    {
        void ApplyHeaders(HttpRequestMessage message, string sessionId, Dictionary<string, string> HttpHeaders);
    }
}
