using LPS.Domain.Common.Interfaces;
using LPS.Domain.Common;
using LPS.Domain;
using LPS.Infrastructure.Caching;
using LPS.Infrastructure.LPSClients.SampleResponseServices;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using System.Buffers;

namespace LPS.Infrastructure.LPSClients.ResponseService
{
    public class ResponseProcessingService : IResponseProcessingService
    {
        private readonly ICacheService<string> _memoryCacheService;
        private static readonly ArrayPool<byte> _bufferPool = ArrayPool<byte>.Shared;
        private readonly IResponseProcessorFactory _responseProcessorFactory;
        private readonly ILogger _logger;
        private readonly IRuntimeOperationIdProvider _runtimeOperationIdProvider;

        public ResponseProcessingService(
            ICacheService<string> memoryCacheService,
            ILogger logger,
            IRuntimeOperationIdProvider runtimeOperationIdProvider,
            IResponseProcessorFactory responseProcessorFactory = null)
        {
            _memoryCacheService = memoryCacheService;
            _logger = logger;
            _runtimeOperationIdProvider = runtimeOperationIdProvider;
            _responseProcessorFactory = responseProcessorFactory ?? new ResponseProcessorFactory(_runtimeOperationIdProvider, _logger, _memoryCacheService);
        }
        public double AverageDataReceived => _averageDataReceived;
        public double SumDataReceived => _sumDataReceived;
        double _averageDataReceived;
        long _sumDataReceived;
        int _responseCount;
        
        SemaphoreSlim _semaphoreSlim = new SemaphoreSlim(1, 1);
        public async Task<(HttpResponse.SetupCommand command, TimeSpan streamTime)> ProcessResponseAsync(
            HttpResponseMessage response,
            HttpRequestProfile lpsHttpRequestProfile,
            CancellationToken token)
        {
            Stopwatch streamStopwatch = Stopwatch.StartNew();
            string contentType = response?.Content?.Headers?.ContentType?.MediaType;
            MimeType mimeType = MimeTypeExtensions.FromContentType(contentType);

            try
            {
                if (response.Content == null)
                {
                    throw new InvalidOperationException("Response content is null.");
                }

                // Calculate the headers size (both response and content headers)
                long responseSize = CalculateHeadersSize(response);
                string cacheKey = $"HtmlContent_{lpsHttpRequestProfile.Id}";
                string content = await _memoryCacheService.GetItemAsync(cacheKey);

                using Stream contentStream = await response.Content.ReadAsStreamAsync(token).ConfigureAwait(false);
                using var timeoutCts = new CancellationTokenSource();
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(token, timeoutCts.Token);
                timeoutCts.CancelAfter(TimeSpan.FromSeconds(30));

                MemoryStream memoryStream = null;
                byte[] buffer = _bufferPool.Rent(64000);
                string locationToResponse = null;
                bool isSemaphoreAcquired = false;
                try
                {
                    // Initialize memoryStream if caching is needed
                    if (content == null && mimeType == MimeType.TextHtml && lpsHttpRequestProfile.DownloadHtmlEmbeddedResources)
                    {
                        memoryStream = new MemoryStream();
                    }

                    // Get the response processor
                    IResponseProcessor responseProcessor = await _responseProcessorFactory.CreateResponseProcessorAsync(
                        lpsHttpRequestProfile.URL, mimeType, lpsHttpRequestProfile.SaveResponse, token);

                    await using (responseProcessor.ConfigureAwait(false))
                    {
                        int bytesRead;
                        streamStopwatch.Start();

                        while ((bytesRead = await contentStream.ReadAsync(buffer.AsMemory(0, buffer.Length), linkedCts.Token)) > 0)
                        {
                            responseSize += bytesRead;
                            streamStopwatch.Stop();

                            // Write to memoryStream for caching
                            if (memoryStream != null)
                            {
                                await memoryStream.WriteAsync(buffer.AsMemory(0, bytesRead), linkedCts.Token);
                            }

                            // Process the chunk with the responseProcessor
                            await responseProcessor.ProcessResponseChunkAsync(buffer, 0, bytesRead, token);

                            streamStopwatch.Start();
                        }

                        streamStopwatch.Stop();

                        // Get the response file path if available
                        locationToResponse = responseProcessor.ResponseFilePath;
                    }

                    // Cache the content once fully read
                    if (memoryStream != null)
                    {
                        content = System.Text.Encoding.UTF8.GetString(memoryStream.ToArray());
                        await _memoryCacheService.SetItemAsync(cacheKey, content);
                    }
                    await _semaphoreSlim.WaitAsync(token);
                    isSemaphoreAcquired = true;
                    _responseCount++;
                    _sumDataReceived += responseSize;
                    _averageDataReceived = _sumDataReceived / _responseCount;
                }
                finally
                {
                    if(isSemaphoreAcquired) 
                        _semaphoreSlim.Release();

                    _bufferPool.Return(buffer);
                    if (memoryStream != null)
                    {
                        await memoryStream.DisposeAsync();
                    }
                }

                return (new HttpResponse.SetupCommand
                {
                    StatusCode = response.StatusCode,
                    StatusMessage = response.ReasonPhrase,
                    LocationToResponse = locationToResponse,
                    IsSuccessStatusCode = response.IsSuccessStatusCode,
                    ResponseContentHeaders = response.Content?.Headers?.ToDictionary(header => header.Key, header => string.Join(", ", header.Value)),
                    ResponseHeaders = response.Headers?.ToDictionary(header => header.Key, header => string.Join(", ", header.Value)),
                    ContentType = mimeType,
                    LPSHttpRequestProfileId = lpsHttpRequestProfile.Id,
                }, streamStopwatch.Elapsed);
            }
            catch (Exception ex)
            {
                await _logger.LogAsync(_runtimeOperationIdProvider.OperationId, $"Error in ProcessResponseAsync: {ex.Message}", LPSLoggingLevel.Error, token);
                throw;
            }
        }

        private long CalculateHeadersSize(HttpResponseMessage response)
        {
            long size = 0;

            // Calculate size of response headers
            foreach (var header in response.Headers)
            {
                size += Encoding.UTF8.GetByteCount(header.Key);
                size += header.Value.Sum(v => Encoding.UTF8.GetByteCount(v));
            }

            // Calculate size of content headers
            if (response.Content?.Headers != null)
            {
                foreach (var contentHeader in response.Content.Headers)
                {
                    size += Encoding.UTF8.GetByteCount(contentHeader.Key);
                    size += contentHeader.Value.Sum(v => Encoding.UTF8.GetByteCount(v));
                }
            }

            return size;
        }

    }

}
