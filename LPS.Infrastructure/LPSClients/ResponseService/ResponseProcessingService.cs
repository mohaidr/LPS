using LPS.Domain.Common.Interfaces;
using LPS.Domain.Common;
using LPS.Domain;
using LPS.Infrastructure.Caching;
using LPS.Infrastructure.LPSClients.SampleResponseServices;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Buffers;
using LPS.Infrastructure.LPSClients.URLServices;
using LPS.Infrastructure.LPSClients.SessionManager;
using System.Net;
using LPS.Infrastructure.LPSClients.CachService;
using LPS.Infrastructure.Common.Interfaces;
using LPS.Domain.LPSRequest.LPSHttpRequest;
using AsyncKeyedLock;

namespace LPS.Infrastructure.LPSClients.ResponseService
{
    public class ResponseProcessingService(
        ICacheService<string> memoryCacheService,
        ILogger logger,
        IRuntimeOperationIdProvider runtimeOperationIdProvider,
        IResponsePersistenceFactory responsePersistenceFactory,
        IMetricsService metricsService, IUrlSanitizationService urlSanitizationService) : IResponseProcessingService
    {
        private readonly ICacheService<string> _memoryCacheService = memoryCacheService;
        private static readonly ArrayPool<byte> _bufferPool = ArrayPool<byte>.Shared;
        private readonly IResponsePersistenceFactory _responsePersistenceFactory = responsePersistenceFactory;
        private readonly ILogger _logger = logger;
        private readonly IRuntimeOperationIdProvider _runtimeOperationIdProvider = runtimeOperationIdProvider;
        private readonly IMetricsService _metricsService = metricsService;
        private readonly IUrlSanitizationService _urlSanitizationService = urlSanitizationService;
        private static readonly AsyncKeyedLocker<string> _locker = new();
        public async Task<(HttpResponse.SetupCommand command, double dataReceivedSize, TimeSpan streamTime)> ProcessResponseAsync(
            HttpResponseMessage responseMessage,
            HttpRequest httpRequest,
            bool cacheResponse,
            CancellationToken token)
        {
            Stopwatch streamStopwatch = Stopwatch.StartNew();
            Stopwatch downStreamWatch = Stopwatch.StartNew();
            string contentType = responseMessage?.Content?.Headers?.ContentType?.MediaType;
            MimeType mimeType = MimeTypeExtensions.FromContentType(contentType);
            IResponsePersistence responsePersistence = null;
            long transferredSize = 0;
            try
            {
                string locationToResponse = string.Empty;
                if (responseMessage.Content == null)
                {
                    throw new InvalidOperationException("Response content is null.");
                }
                var statusLine = $"HTTP/{responseMessage.Version} {(int)responseMessage.StatusCode} {responseMessage.ReasonPhrase}\r\n";
                transferredSize = Encoding.UTF8.GetByteCount(statusLine);
                if (!(responseMessage.StatusCode == HttpStatusCode.NotModified || responseMessage.StatusCode == HttpStatusCode.NoContent))
                {
                    // Calculate the headers size (both response and content headers)
                    transferredSize += CalculateHeadersSize(responseMessage);
                    string cacheKey = $"{CachePrefixes.Content}{httpRequest.Id}";
                    string content = await _memoryCacheService.GetItemAsync(cacheKey);

                    using Stream contentStream = await responseMessage.Content.ReadAsStreamAsync(token).ConfigureAwait(false);
                    MemoryStream memoryStream = null;
                    byte[] buffer = _bufferPool.Rent(64000);
                    string sampleResponseCacheKey = $"{CachePrefixes.SampleResponse}{httpRequest.Id}";

                    try
                    {
                        // Initialize memoryStream if caching is needed
                        if (content == null && cacheResponse)
                        {
                            memoryStream = new MemoryStream();
                        }

                        using (await _locker.LockAsync(sampleResponseCacheKey, token))
                        {
                            // Get the response processor
                            if (httpRequest.SaveResponse && !_memoryCacheService.TryGetItem(sampleResponseCacheKey, out _))
                            {
                                var destination = GetDestination(httpRequest, mimeType);
                                responsePersistence = await _responsePersistenceFactory.CreateAsync(destination, PersistenceType.File, token);
                                await _memoryCacheService.SetItemAsync(sampleResponseCacheKey, destination, TimeSpan.MaxValue); // once dispose is called (mening that all data written to the file we set it to the default to save after some time again)
                            }
                        }

                        int bytesRead;
                        streamStopwatch.Restart();
                        downStreamWatch.Restart();
                        while ((bytesRead = await contentStream.ReadAsync(buffer.AsMemory(0, buffer.Length), token)) > 0)
                        {
                            transferredSize += bytesRead;
                            await _metricsService.TryUpdateDataReceivedAsync(httpRequest.Id, bytesRead, token);

                            // Write to memoryStream for caching
                            if (memoryStream != null)
                            {
                                await memoryStream.WriteAsync(buffer.AsMemory(0, bytesRead), token);
                            }

                            if (responsePersistence != null)
                            {
                                // Process the chunk with the responseProcessor
                                await responsePersistence.PersistResponseChunkAsync(buffer, 0, bytesRead, token);
                            }

                            streamStopwatch.Restart();
                            // Get the response file path if available
                            locationToResponse = responsePersistence?.ResponseFilePath;
                        }
                        downStreamWatch.Stop();
                        // Cache the content once fully read
                        if (memoryStream != null)
                        {
                            content = Encoding.UTF8.GetString(memoryStream.ToArray());
                            await _memoryCacheService.SetItemAsync(cacheKey, content);
                        }
                    }
                    finally
                    {
                        _bufferPool.Return(buffer);
                        if (memoryStream != null)
                        {
                            await memoryStream.DisposeAsync();
                        }
                        if (responsePersistence != null)
                        {
                            await _memoryCacheService.SetItemAsync(sampleResponseCacheKey, responsePersistence.ResponseFilePath); // All data written to the file, we now set it to the default to save after 30 seconds
                            await responsePersistence.DisposeAsync();
                        }
                    }

                }

                return (new HttpResponse.SetupCommand
                {
                    StatusCode = responseMessage.StatusCode,
                    StatusMessage = responseMessage.ReasonPhrase,
                    LocationToResponse = locationToResponse,
                    IsSuccessStatusCode = responseMessage.IsSuccessStatusCode,
                    ResponseContentHeaders = responseMessage.Content?.Headers?.ToDictionary(header => header.Key, header => string.Join(", ", header.Value)),
                    ResponseHeaders = responseMessage.Headers?.ToDictionary(header => header.Key, header => string.Join(", ", header.Value)),
                    ContentType = mimeType,
                    HttpRequestId = httpRequest.Id,
                }, transferredSize, downStreamWatch.Elapsed);
            }
            catch (Exception ex)
            {
                await _logger.LogAsync(_runtimeOperationIdProvider.OperationId, $"Error in ProcessResponseAsync: {ex.Message}", LPSLoggingLevel.Error, token);     
                return (new HttpResponse.SetupCommand
                {
                    StatusCode = 0,
                    StatusMessage = ex?.InnerException?.Message ?? ex?.Message,
                    LocationToResponse = string.Empty,
                    IsSuccessStatusCode = false,
                    HttpRequestId = httpRequest.Id,
                }, transferredSize, downStreamWatch.Elapsed);
            }
        }
        private string GetDestination(HttpRequest httpRequest, MimeType mimeType)
        {
            string sanitizedUrl = _urlSanitizationService.Sanitize(httpRequest.Url.Url);
            string directoryName = $"{sanitizedUrl}.{_runtimeOperationIdProvider.OperationId}.Resources";
            Directory.CreateDirectory(directoryName);
            string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss-fff");
            string destination = Path.Combine(directoryName, $"{sanitizedUrl}_{timestamp}{mimeType.ToFileExtension()}");
            return destination;
        }

        private static long CalculateHeadersSize(HttpResponseMessage response)
        {
            long size = 0;

            // Calculate size of response headers
            foreach (var header in response.Headers)
            {
                // Include CRLF at the end of each header line
                size += Encoding.UTF8.GetByteCount($"{header.Key}: {string.Join(", ", header.Value)}\r\n");
            }

            // Calculate size of content headers
            if (response.Content?.Headers != null)
            {
                foreach (var contentHeader in response.Content.Headers)
                {
                    // Include CRLF at the end of each content header line
                    size += Encoding.UTF8.GetByteCount($"{contentHeader.Key}: {string.Join(", ", contentHeader.Value)}\r\n");
                }
            }

            // Include an additional CRLF after the headers section to separate headers from the body
            size += Encoding.UTF8.GetByteCount("\r\n");

            return size;
        }

    }

}
