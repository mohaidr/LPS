using LPS.Domain;
using LPS.Infrastructure.LPSClients.HeaderServices;
using LPS.Infrastructure.Caching;
using System;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using System.Net;
using LPS.Domain.Common.Interfaces;
using System.Collections.Generic;
using LPS.Domain.LPSSession;
using System.Net.Http.Headers;
using LPS.Infrastructure.LPSClients.CachService;
using System.Net.Mime;

namespace LPS.Infrastructure.LPSClients.MessageServices
{
    public class MessageService(IHttpHeadersService headersService,
                                ILogger logger,
                                IRuntimeOperationIdProvider runtimeOperationIdProvider,
                                ICacheService<long> memoryCacheService,
                                ICacheService<object> dynamicTypeMemoryCacheService,
                                IPlaceholderResolverService placeHolderResolver) : IMessageService
    {
        readonly ILogger _logger = logger;
        readonly IRuntimeOperationIdProvider _runtimeOperationIdProvider = runtimeOperationIdProvider;
        readonly IHttpHeadersService _headersService = headersService;
        readonly ICacheService<long> _memoryCacheService = memoryCacheService;
        readonly ICacheService<object> _dynamicTypeCacheService = dynamicTypeMemoryCacheService;
        readonly IPlaceholderResolverService _placeHolderResolver = placeHolderResolver;

        public async Task<(HttpRequestMessage HttpRequestMessage, long MessageSize)> BuildAsync(HttpRequest httpRequest, string sessionId, CancellationToken token = default)
        {
            // Resolve placeholders for HttpVersion, HttpMethod, URL
            var resolvedHttpVersion = await _placeHolderResolver.ResolvePlaceholdersAsync<string>(httpRequest.HttpVersion, sessionId, token);
            var resolvedHttpMethod = await _placeHolderResolver.ResolvePlaceholdersAsync<string>(httpRequest.HttpMethod, sessionId, token);
            var resolvedUrl = await _placeHolderResolver.ResolvePlaceholdersAsync<string>(httpRequest.Url.Url, sessionId, token);
            bool supportsContent = SupportsContent(resolvedHttpMethod);
            var raw = supportsContent && httpRequest.Payload?.Type == Payload.PayloadType.Raw
                ? await _placeHolderResolver.ResolvePlaceholdersAsync<string>(httpRequest.Payload.RawValue, sessionId, token) ?? string.Empty
                : null;
            var httpRequestMessage = CreateRequestMessage(resolvedUrl, resolvedHttpMethod, resolvedHttpVersion, httpRequest.SupportH2C == true, raw);

            if (supportsContent && httpRequest.Payload != null && httpRequest.Payload.Type != Payload.PayloadType.Raw)
            {
                switch (httpRequest.Payload.Type)
                {
                    case Payload.PayloadType.Multipart:
                        var multipartContent = new MultipartFormDataContent();
                        // Add fields
                        foreach (var field in httpRequest.Payload.Multipart.Fields)
                        {
                            var name = await _placeHolderResolver.ResolvePlaceholdersAsync<string>(field.Name, sessionId, token);
                            var value = await _placeHolderResolver.ResolvePlaceholdersAsync<string>(field.Value, sessionId, token) ?? string.Empty;

                            var resolvedCt = await _placeHolderResolver.ResolvePlaceholdersAsync<string>(field.ContentType, sessionId, token);
                            var contentType = string.IsNullOrWhiteSpace(resolvedCt) ? "text/plain" : resolvedCt;

                            var content = new StringContent(value, Encoding.UTF8); // adds text/plain; charset=utf-8
                            if (MediaTypeHeaderValue.TryParse(contentType, out var mt))
                            {
                                // ensure charset for text/*
                                // adding mt.CharSet is not recommended with application/json for example
                                if (mt.MediaType?.StartsWith("text/", StringComparison.OrdinalIgnoreCase) == true && string.IsNullOrEmpty(mt.CharSet))
                                    mt.CharSet = "utf-8";
                                content.Headers.ContentType = mt;
                            }

                            multipartContent.Add(content, name);
                        }

                        foreach (var file in httpRequest.Payload.Multipart.Files)
                        {
                            HttpContent fileContent;

                            if (file.Content is byte[] binaryContent)
                            {
                                // Use ByteArrayContent without copying the array
                                fileContent = new ByteArrayContent(binaryContent);
                                if (MediaTypeHeaderValue.TryParse(file.ContentType, out var mt))
                                {
                                    fileContent.Headers.ContentType = mt;
                                }
                                else
                                {
                                    fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
                                }
                            }
                            else if (file.Content is string textContent)
                            {
                                string multipartFileCacheKey = $"{CachePrefixes.Multipartfile}{file.Name}";
                                fileContent = (await _dynamicTypeCacheService.GetItemAsync(multipartFileCacheKey)) as HttpContent;
                                if (fileContent is null)
                                {
                                    fileContent = new StringContent(textContent, Encoding.UTF8); // adds text/plain; charset=utf-8
                                    if (MediaTypeHeaderValue.TryParse(file.ContentType, out var mt))
                                    {
                                        // ensure charset for text/*
                                        // adding mt.CharSet is not recommended with application/json for example
                                        if (mt.MediaType?.StartsWith("text/", StringComparison.OrdinalIgnoreCase) == true && string.IsNullOrEmpty(mt.CharSet))
                                            mt.CharSet = "utf-8";
                                        fileContent.Headers.ContentType = mt;
                                    }
                                }
                                await _dynamicTypeCacheService.SetItemAsync(multipartFileCacheKey, fileContent, TimeSpan.FromMinutes(5));
                            }
                            else
                            {
                                throw new InvalidOperationException($"Unsupported file content type for file: {file.Name}");
                            }

                            fileContent.Headers.ContentType = new MediaTypeHeaderValue(file.ContentType);
                            multipartContent.Add(fileContent, file.Name, file.Name);
                        }

                        httpRequestMessage.Content = multipartContent;
                        break;

                    case Payload.PayloadType.Binary:
                        var binaryData = httpRequest.Payload.BinaryValue;
                        httpRequestMessage.Content = new ByteArrayContent(binaryData)
                        {
                            Headers = { ContentType = new MediaTypeHeaderValue("application/octet-stream") }
                        };
                        break;

                    default:
                        throw new NotSupportedException($"Unsupported payload type: {httpRequest.Payload.Type}");
                }
            }

            var requestedVersion = GetHttpVersion(resolvedHttpVersion);
            if (httpRequest.SupportH2C == true && requestedVersion != HttpVersion.Version20)
            {
                await _logger.LogAsync(_runtimeOperationIdProvider.OperationId,
                    $"SupportH2C was enabled on a non-HTTP/2 protocol, so the version is being overridden from {requestedVersion} to {HttpVersion.Version20}.",
                    LPSLoggingLevel.Warning, token);
            }

            // Apply headers to the request
            await _headersService.ApplyHeadersAsync(httpRequestMessage, sessionId, httpRequest.HttpHeaders, token);

            // Cache key to identify the request profile
            string cacheKey = $"{CachePrefixes.RequestSize}{httpRequest.Id}";

            // Check if the message size is cached
            if (!_memoryCacheService.TryGetItem(cacheKey, out long messageSize))
            {
                // If not cached, calculate the message size based on the profile
                messageSize = await CalculateRequestSizeAsync(httpRequestMessage);

                // Cache the calculated size
                await _memoryCacheService.SetItemAsync(cacheKey, messageSize);
            }

            // Update the DataSent metric using MetricsService

            return (httpRequestMessage, messageSize);
        }

        public static bool SupportsContent(string method) => method.Equals("POST", StringComparison.OrdinalIgnoreCase)
            || method.Equals("PUT", StringComparison.OrdinalIgnoreCase)
            || method.Equals("PATCH", StringComparison.OrdinalIgnoreCase);

        public static HttpRequestMessage CreateRequestMessage(string url, string method, string version, bool supportH2C, string rawPayload = null)
        {
            var message = new HttpRequestMessage(new HttpMethod(method), new Uri(url))
            {
                Version = supportH2C ? HttpVersion.Version20 : GetHttpVersion(version),
                VersionPolicy = supportH2C ? HttpVersionPolicy.RequestVersionExact : HttpVersionPolicy.RequestVersionOrLower
            };
            if (SupportsContent(method) && rawPayload != null)
                message.Content = new ByteArrayContent(Encoding.UTF8.GetBytes(rawPayload));
            return message;
        }

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
