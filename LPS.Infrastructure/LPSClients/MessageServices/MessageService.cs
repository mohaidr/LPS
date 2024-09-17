using LPS.Domain;
using LPS.Infrastructure.LPSClients.HeaderServices;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace LPS.Infrastructure.LPSClients.MessageServices
{
    public class MessageService: IMessageService
    {
        IHttpHeadersService _headersService;
        public MessageService(IHttpHeadersService headersService) 
        { 
            _headersService = headersService;
        }
        public HttpRequestMessage Build(HttpRequestProfile lpsHttpRequestProfile)
        {
            var httpRequestMessage = new HttpRequestMessage();
            httpRequestMessage.RequestUri = new Uri(lpsHttpRequestProfile.URL);
            httpRequestMessage.Method = new HttpMethod(lpsHttpRequestProfile.HttpMethod);
            bool supportsContent = lpsHttpRequestProfile.HttpMethod.ToLower() == "post" || lpsHttpRequestProfile.HttpMethod.ToLower() == "put" || lpsHttpRequestProfile.HttpMethod.ToLower() == "patch";
            httpRequestMessage.Version = GetHttpVersion(lpsHttpRequestProfile.Httpversion);
            httpRequestMessage.Content = supportsContent ? new StringContent(lpsHttpRequestProfile.Payload ?? string.Empty) : null;
            _headersService.ApplyHeaders(httpRequestMessage, lpsHttpRequestProfile.HttpHeaders);
            return httpRequestMessage;
        }
        private static Version GetHttpVersion(string version)
        {
            return version switch
            {
                "1.0" => HttpVersion.Version10,
                "1.1" => HttpVersion.Version11,
                "2.0" => HttpVersion.Version20,
                _ => HttpVersion.Version20,
            };
        }
    }
}
