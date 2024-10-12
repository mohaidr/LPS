using LPS.Domain;
using LPS.Infrastructure.LPSClients.HeaderServices;
using LPS.Infrastructure.LPSClients.Metrics;
using LPS.Infrastructure.Caching;
using System;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using LPS.Infrastructure.LPSClients.MetricsServices;
using System.Net;
using Microsoft.Extensions.Caching.Memory;

namespace LPS.Infrastructure.LPSClients.MessageServices
{
    public class MessageService(IHttpHeadersService headersService,
                                IMetricsService metricsService,
                                ICacheService<long> memoryCacheService = null) : IMessageService
    {
        readonly IHttpHeadersService _headersService = headersService;
        readonly IMetricsService _metricsService = metricsService;
        readonly ICacheService<long> _memoryCacheService = memoryCacheService ?? new MemoryCacheService<long>(new MemoryCache(new MemoryCacheOptions
        {
            SizeLimit = 1024
        }));

        public async Task<HttpRequestMessage> BuildAsync(HttpRequestProfile lpsHttpRequestProfile, CancellationToken token = default)
        {
            var httpRequestMessage = new HttpRequestMessage
            {
                RequestUri = new Uri(lpsHttpRequestProfile.URL),
                Method = new HttpMethod(lpsHttpRequestProfile.HttpMethod)
            };

            bool supportsContent = lpsHttpRequestProfile.HttpMethod.Equals("post", StringComparison.CurrentCultureIgnoreCase) || lpsHttpRequestProfile.HttpMethod.Equals("put", StringComparison.CurrentCultureIgnoreCase) || lpsHttpRequestProfile.HttpMethod.ToLower() == "patch";
            httpRequestMessage.Version = GetHttpVersion(lpsHttpRequestProfile.Httpversion);
            httpRequestMessage.Content = supportsContent ? new StringContent(lpsHttpRequestProfile.Payload ?? string.Empty) : null;

            // Apply headers to the request
            _headersService.ApplyHeaders(httpRequestMessage, lpsHttpRequestProfile.HttpHeaders);

            // Cache key to identify the request profile
            string cacheKey = lpsHttpRequestProfile.Id.ToString();

            // Check if the message size is cached
            if (!_memoryCacheService.TryGetItem(cacheKey, out long messageSize))
            {
                // If not cached, calculate the message size based on the profile
                messageSize = CalculateMessageSize(lpsHttpRequestProfile);

                // Cache the calculated size
                await _memoryCacheService.SetItemAsync(cacheKey, messageSize);
            }

            // Update the DataSent metric using MetricsService
            await _metricsService.TryUpdateDataSentAsync(lpsHttpRequestProfile.Id, messageSize, token);

            return httpRequestMessage;
        }

        private static long CalculateMessageSize(HttpRequestProfile profile)
        {
            long size = 0;

            // Calculate the size of the request line (HTTP method + URL + version)
            size += Encoding.UTF8.GetByteCount($"{profile.HttpMethod} {profile.URL} HTTP/{profile.Httpversion}\r\n");

            // Calculate the size of the headers in the profile
            foreach (var header in profile.HttpHeaders)
            {
                size += Encoding.UTF8.GetByteCount(header.Key) + Encoding.UTF8.GetByteCount(header.Value) + 4; // +4 for ': ' and '\r\n'
            }

            // If there are no headers, add a basic "Host" header estimation (or any implicit headers)
            if (!profile.HttpHeaders.Any())
            {
                size += Encoding.UTF8.GetByteCount("Host: ") + Encoding.UTF8.GetByteCount(new Uri(profile.URL).Host) + 2; // Assuming "Host" header is always present
            }

            // Add the terminating CRLF for the header section
            size += 2;

            // Calculate the size of the payload (if it exists)
            if (!string.IsNullOrEmpty(profile.Payload))
            {
                size += Encoding.UTF8.GetByteCount(profile.Payload);
            }

            return size;
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
