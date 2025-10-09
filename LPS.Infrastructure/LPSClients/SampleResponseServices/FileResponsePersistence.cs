// FileResponseProcessor.cs
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using LPS.Domain.Common.Interfaces;
using LPS.Infrastructure.Caching;
using LPS.Infrastructure.LPSClients.URLServices;
using System.Collections.Concurrent;
using LPS.Infrastructure.LPSClients.CachService;
using System.Net.Http;
using AsyncKeyedLock;
using LPS.Domain;

namespace LPS.Infrastructure.LPSClients.SampleResponseServices
{
    public class FileResponsePersistence(
        ICacheService<string> memoryCache,
        ILogger logger,
        IRuntimeOperationIdProvider runtimeOperationIdProvider) : IResponsePersistence
    {

        private FileStream _fileStream;
        private readonly ICacheService<string> _memoryCache = memoryCache;
        private readonly ILogger _logger = logger;
        private readonly IRuntimeOperationIdProvider _runtimeOperationIdProvider = runtimeOperationIdProvider;
        private bool _disposed = false;
        private bool _isInitialized = false;
        public string ResponseFilePath { get; private set; }
        /// <summary>
        /// Initializes the FileStream and updates the cache with no expiration.
        /// This method manages the semaphore internally.
        /// </summary>
        public async Task InitializeAsync(string processTo, CancellationToken token)
        {
            try
            {

                _fileStream = new FileStream(processTo, FileMode.Create, FileAccess.Write, FileShare.None, 4096, useAsync: true);
                ResponseFilePath = processTo;
                _isInitialized = true;
            }
            catch (Exception ex)
            {
                await _logger.LogAsync(
                    _runtimeOperationIdProvider.OperationId,
                    $"Failed to initialize FileResponsePersistence for {ResponseFilePath}: {ex.Message}",
                    LPSLoggingLevel.Error,
                    token);

            }
        }

        public async Task PersistResponseChunkAsync(byte[] buffer, int offset, int count, CancellationToken token)
        {
            ObjectDisposedException.ThrowIf(_disposed, nameof(FileResponsePersistence));
            if (!_isInitialized)
            {
                // No-op processor; do nothing
                return;
            }
            try
            {
                await _fileStream.WriteAsync(buffer.AsMemory(offset, count), token);
            }
            catch (Exception ex)
            {
                // On failure, remove cache entry and log the error
                await _logger.LogAsync(
                    _runtimeOperationIdProvider.OperationId,
                    $"Failed to write response chunk to {ResponseFilePath}: {ex.Message}",
                    LPSLoggingLevel.Error,
                    CancellationToken.None);
            }
        }

        public async ValueTask DisposeAsync()
        {
            if (!_disposed)
            {
                _disposed = true;

                if (_isInitialized)
                {
                    try
                    {
                        await _fileStream.FlushAsync();
                        await _fileStream.DisposeAsync();

                        // Update cache entry with default cache duration
                        await _logger.LogAsync(
                            _runtimeOperationIdProvider.OperationId,
                            $"Sample response saved to: {ResponseFilePath}",
                            LPSLoggingLevel.Verbose,
                            CancellationToken.None);
                    }
                    catch (Exception ex)
                    {
                        await _logger.LogAsync(
                            _runtimeOperationIdProvider.OperationId,
                            $"Error during disposal of FileResponsePersistence for URL {ResponseFilePath}: {ex.Message}",
                            LPSLoggingLevel.Error,
                            CancellationToken.None);
                    }
                }
            }
        }
    }
}
