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
using System.Collections.Generic;

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
        readonly IPlaceholderResolverService _placeHolderResolver = placeHolderResolver;

        public async Task<HttpRequestMessage> BuildAsync(HttpRequest httpRequest, string sessionId, CancellationToken token = default)
        {
            // Resolve placeholders for HttpVersion, HttpMethod, URL, and Payload
            var resolvedHttpVersion = await _placeHolderResolver.ResolvePlaceholdersAsync<string>(httpRequest.HttpVersion, sessionId, token);
            var resolvedHttpMethod = await _placeHolderResolver.ResolvePlaceholdersAsync<string>(httpRequest.HttpMethod, sessionId, token);
            var resolvedUrl = await _placeHolderResolver.ResolvePlaceholdersAsync<string>(httpRequest.URL, sessionId, token);
            var resolvedContent = (await _placeHolderResolver.ResolvePlaceholdersAsync<string>(httpRequest.Payload, sessionId, token)) ?? string.Empty;

            // Create the HttpRequestMessage with resolved values
            var httpRequestMessage = new HttpRequestMessage
            {
                RequestUri = new Uri(resolvedUrl),
                Method = new HttpMethod(resolvedHttpMethod),
                Version = GetHttpVersion(resolvedHttpVersion)
            };

            // Determine if the request supports content
            bool supportsContent = resolvedHttpMethod.Equals("post", StringComparison.CurrentCultureIgnoreCase)
                                   || resolvedHttpMethod.Equals("put", StringComparison.CurrentCultureIgnoreCase)
                                   || resolvedHttpMethod.Equals("patch", StringComparison.CurrentCultureIgnoreCase);

            if (httpRequest.SupportH2C.HasValue && httpRequest.SupportH2C.Value)
            {
                if (httpRequestMessage.Version != HttpVersion.Version20)
                {
                    await _logger.LogAsync(_runtimeOperationIdProvider.OperationId,
                        $"SupportH2C was enabled on a non-HTTP/2 protocol, so the version is being overridden from {httpRequestMessage.Version} to {HttpVersion.Version20}.",
                        LPSLoggingLevel.Warning, token);
                    httpRequestMessage.Version = HttpVersion.Version20;
                }
                httpRequestMessage.VersionPolicy = HttpVersionPolicy.RequestVersionExact;
            }

            httpRequestMessage.Content = supportsContent ? new StringContent(resolvedContent) : null;

            // Apply headers to the request
           await _headersService.ApplyHeadersAsync(httpRequestMessage, sessionId, httpRequest.HttpHeaders, token);

            // Cache key to identify the request profile
            string cacheKey = $"request_size_{httpRequest.Id}";

            // Check if the message size is cached
            if (!_memoryCacheService.TryGetItem(cacheKey, out long messageSize))
            {
                // If not cached, calculate the message size based on the profile
                messageSize = await CalculateRequestSizeAsync(httpRequestMessage);

                // Cache the calculated size
                await _memoryCacheService.SetItemAsync(cacheKey, messageSize);
            }

            // Update the DataSent metric using MetricsService
            await _metricsService.TryUpdateDataSentAsync(httpRequest.Id, messageSize, token);

            return httpRequestMessage;
        }
        //TODO: Move to a separate service
        private static async Task<long> CalculateRequestSizeAsync(HttpRequestMessage httpRequestMessage)
        {
            long size = 0;

            // Start-Line Size
            size += Encoding.UTF8.GetByteCount(
                $"{httpRequestMessage.Method.Method} {httpRequestMessage.RequestUri?.ToString() ?? string.Empty} HTTP/{httpRequestMessage.Version}\r\n"
            );

            // Headers Size
            var headers = httpRequestMessage.Headers.ToDictionary(h => h.Key, h => string.Join(", ", h.Value));

            // Content headers (if present)
            if (httpRequestMessage.Content?.Headers != null)
            {
                foreach (var header in httpRequestMessage.Content.Headers)
                {
                    headers[header.Key] = string.Join(", ", header.Value);
                }
            }

            // Add all headers
            foreach (var header in headers)
            {
                size += Encoding.UTF8.GetByteCount($"{header.Key}: {header.Value}\r\n");
            }

            // Add Host header if missing
            if (!headers.Any(header => header.Key.Equals("Host", StringComparison.OrdinalIgnoreCase)))
            {
                size += Encoding.UTF8.GetByteCount("Host: ") + Encoding.UTF8.GetByteCount(new Uri(httpRequestMessage.RequestUri?.ToString() ?? string.Empty).Host) + 2;
            }

            // Final \r\n after headers
            size += 2;

            // Content Size (if present)
            if (httpRequestMessage.Content != null)
            {
                var contentBytes = await httpRequestMessage.Content.ReadAsByteArrayAsync();
                size += contentBytes.Length;
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
