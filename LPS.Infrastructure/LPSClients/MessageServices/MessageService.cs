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
using LPS.Domain.Common.Interfaces;
using LPS.Infrastructure.LPSClients.PlaceHolderService;
using System.Security.Cryptography.X509Certificates;

namespace LPS.Infrastructure.LPSClients.MessageServices
{
    public class MessageService(IHttpHeadersService headersService,
                                IMetricsService metricsService,
                                ILogger logger,
                                IRuntimeOperationIdProvider runtimeOperationIdProvider,
                                ICacheService<long> memoryCacheService,
                                IPlaceholderResolverService placeHolderResolver) : IMessageService
    {
        readonly ILogger _logger = logger;
        readonly IRuntimeOperationIdProvider _runtimeOperationIdProvider = runtimeOperationIdProvider;
        readonly IHttpHeadersService _headersService = headersService;
        readonly IMetricsService _metricsService = metricsService;
        readonly ICacheService<long> _memoryCacheService = memoryCacheService;
        readonly IPlaceholderResolverService _placeHolderResolver= placeHolderResolver;
        public async Task<HttpRequestMessage> BuildAsync(HttpRequest httpRequest, string sessionId, CancellationToken token = default)
        {
            var httpRequestMessage = new HttpRequestMessage
            {
                RequestUri = new Uri(httpRequest.URL),
                Method = new HttpMethod(httpRequest.HttpMethod)
            };

            bool supportsContent = httpRequest.HttpMethod.Equals("post", StringComparison.CurrentCultureIgnoreCase) || httpRequest.HttpMethod.Equals("put", StringComparison.CurrentCultureIgnoreCase) || httpRequest.HttpMethod.ToLower() == "patch";
            httpRequestMessage.Version = GetHttpVersion(httpRequest.HttpVersion);

            if (httpRequest.SupportH2C.HasValue && httpRequest.SupportH2C.Value)
            {
                if (httpRequestMessage.Version != HttpVersion.Version20)
                {
                    await _logger.LogAsync(_runtimeOperationIdProvider.OperationId, $"SupportH2C was enabled on a non-HTTP/2 protocol, so the version is being overridden from {httpRequestMessage.Version} to {HttpVersion.Version20}.", LPSLoggingLevel.Warning, token);
                    httpRequestMessage.Version = HttpVersion.Version20;
                }
                httpRequestMessage.VersionPolicy = HttpVersionPolicy.RequestVersionExact;
            }
            var resolvedContent = _placeHolderResolver.ResolvePlaceholders(httpRequest.Payload, sessionId);

            httpRequestMessage.Content = supportsContent ? new StringContent(resolvedContent) : null;

            // Apply headers to the request
            _headersService.ApplyHeaders(httpRequestMessage, sessionId, httpRequest.HttpHeaders);

            // Cache key to identify the request profile
            string cacheKey = httpRequest.Id.ToString();

            // Check if the message size is cached
            if (!_memoryCacheService.TryGetItem(cacheKey, out long messageSize))
            {
                // If not cached, calculate the message size based on the profile
                messageSize = CalculateMessageSize(httpRequest);

                // Cache the calculated size
                await _memoryCacheService.SetItemAsync(cacheKey, messageSize);
            }

            // Update the DataSent metric using MetricsService
            await _metricsService.TryUpdateDataSentAsync(httpRequest.Id, messageSize, token);

            return httpRequestMessage;
        }

        private static long CalculateMessageSize(HttpRequest profile)
        {
            long size = 0;

            // Calculate the size of the request line (HTTP method + URL + version)
            size += Encoding.UTF8.GetByteCount($"{profile.HttpMethod} {profile.URL} HTTP/{profile.HttpVersion}\r\n");

            // Calculate the size of the headers in the profile
            foreach (var header in profile.HttpHeaders)
            {
                size += Encoding.UTF8.GetByteCount(header.Key) + Encoding.UTF8.GetByteCount(header.Value) + 4; // +4 for ': ' and '\r\n'
            }

            // If there are no headers, add a basic "Host" header estimation (or any implicit headers)
            if (profile.HttpHeaders.Count == 0 || !profile.HttpHeaders.Any(header=> header.Key.Equals("Host", StringComparison.OrdinalIgnoreCase)))
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
