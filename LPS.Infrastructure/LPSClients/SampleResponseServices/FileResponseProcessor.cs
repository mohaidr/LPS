// FileResponseProcessor.cs
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using LPS.Domain.Common.Interfaces;
using LPS.Infrastructure.Caching;
using LPS.Infrastructure.LPSClients.URLServices;
using System.Collections.Concurrent;

namespace LPS.Infrastructure.LPSClients.SampleResponseServices
{
    public class FileResponseProcessor : IResponseProcessor
    {
        private static readonly ConcurrentDictionary<string, SemaphoreSlim> _semaphoreDictionary = new ConcurrentDictionary<string, SemaphoreSlim>();

        private FileStream _fileStream;
        private readonly ICacheService<string> _memoryCache;
        private readonly ILogger _logger;
        private readonly IRuntimeOperationIdProvider _runtimeOperationIdProvider;
        private readonly string _url;
        private bool _disposed = false;
        private bool _isActive = false;

        public string ResponseFilePath { get; private set; }

        public FileResponseProcessor(
            string url,
            ICacheService<string> memoryCache,
            ILogger logger,
            IRuntimeOperationIdProvider runtimeOperationIdProvider)
        {
            _url = url;
            _memoryCache = memoryCache;
            _logger = logger;
            _runtimeOperationIdProvider = runtimeOperationIdProvider;
        }

        /// <summary>
        /// Initializes the FileStream and updates the cache with no expiration.
        /// This method manages the semaphore internally.
        /// </summary>
        public async Task InitializeAsync(string type, CancellationToken token)
        {
            string cacheKey = $"SampleResponse_{_url}";
            var semaphore = _semaphoreDictionary.GetOrAdd(cacheKey, new SemaphoreSlim(1, 1));
            bool lockAcquired = false;
            await semaphore.WaitAsync(token);
            lockAcquired = true;
            try
            {
                if (_memoryCache.TryGetItem(cacheKey, out _))
                {
                    // Response has been saved recently; no need to process
                    _isActive = false;
                    return;
                }
                else
                {
                    // Proceed to initialize processing
                    _isActive = true;

                    // Sanitize the URL and prepare the file path
                    string sanitizedUrl = new UrlSanitizationService().Sanitize(_url);
                    string directoryName = $"{sanitizedUrl}.{_runtimeOperationIdProvider.OperationId}.Resources";
                    Directory.CreateDirectory(directoryName);
                    string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
                    string filePath = Path.Combine(directoryName, $"{sanitizedUrl}_{timestamp}.{type}");

                    _fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, useAsync: true);
                    ResponseFilePath = filePath;

                    // Set cache with no expiration (using TimeSpan.MaxValue)
                    await _memoryCache.SetItemAsync(cacheKey, _url, TimeSpan.MaxValue);
                }
            }
            catch (Exception ex)
            {
                // Remove the semaphore entry and log the error
                _semaphoreDictionary.TryRemove(cacheKey, out _);

                await _logger.LogAsync(
                    _runtimeOperationIdProvider.OperationId,
                    $"Failed to initialize FileResponseProcessor for URL {_url}: {ex.Message}",
                    LPSLoggingLevel.Error,
                    token);
                throw;
            }
            finally {
                if (lockAcquired)
                {
                    semaphore.Release();
                }
            }
        }

        public async Task ProcessResponseChunkAsync(byte[] buffer, int offset, int count, CancellationToken token)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(FileResponseProcessor));

            if (!_isActive)
            {
                // No-op processor; do nothing
                return;
            }

            try
            {
                await _fileStream.WriteAsync(buffer, offset, count, token);
            }
            catch (Exception ex)
            {
                // On failure, remove cache entry and log the error
                string cacheKey = $"SampleResponse_{_url}";
                await _memoryCache.RemoveItemAsync(cacheKey);
                await _logger.LogAsync(
                    _runtimeOperationIdProvider.OperationId,
                    $"Failed to write response chunk for URL {_url}: {ex.Message}",
                    LPSLoggingLevel.Error,
                    CancellationToken.None);
                throw;
            }
        }

        public async ValueTask DisposeAsync()
        {
            if (!_disposed)
            {
                _disposed = true;

                if (_isActive)
                {
                    try
                    {
                        await _fileStream.FlushAsync();
                        await _fileStream.DisposeAsync();

                        string cacheKey = $"SampleResponse_{_url}";
                        // Update cache entry with default cache duration
                        await _memoryCache.SetItemAsync(cacheKey, _url);
                        await _logger.LogAsync(
                            _runtimeOperationIdProvider.OperationId,
                            $"Sample response saved for URL: {_url}",
                            LPSLoggingLevel.Verbose,
                            CancellationToken.None);
                    }
                    catch (Exception ex)
                    {
                        await _logger.LogAsync(
                            _runtimeOperationIdProvider.OperationId,
                            $"Error during disposal of FileResponseProcessor for URL {_url}: {ex.Message}",
                            LPSLoggingLevel.Error,
                            CancellationToken.None);
                        throw;
                    }
                    finally
                    {
                        // Release the semaphore
                        string  releaseCacheKey = $"SampleResponse_{_url}";
                        _semaphoreDictionary.TryRemove(releaseCacheKey, out _);
                    }
                }
            }
        }
    }
}
