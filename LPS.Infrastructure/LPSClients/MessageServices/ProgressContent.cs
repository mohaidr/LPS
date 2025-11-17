using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Buffers;
using System.Threading;
using YamlDotNet.Core.Tokens;
using System.Diagnostics;
using LPS.Infrastructure.Common.Interfaces;
using LPS.Domain;
using LPS.Infrastructure.Monitoring.Metrics;

namespace LPS.Infrastructure.LPSClients.MessageServices
{
    public class ProgressContent : HttpContent
    {
        private readonly HttpContent _originalContent;
        private readonly IProgress<long> _progress;
        private readonly HttpRequestMessage _httpRequestMessage;
        private static readonly ArrayPool<byte> _bufferPool = ArrayPool<byte>.Shared;
        CancellationToken _token;
        
        public ProgressContent(HttpContent content, IProgress<long> progress, HttpRequestMessage httpRequestMessage, CancellationToken token)
        {
            _token = token;
            _originalContent = content ?? throw new ArgumentNullException(nameof(content));
            _progress = progress ?? throw new ArgumentNullException(nameof(progress));
            _httpRequestMessage = httpRequestMessage ?? throw new ArgumentNullException(nameof(httpRequestMessage));
            
            foreach (var header in _originalContent.Headers)
            {
                Headers.Add(header.Key, header.Value);
            }
        }

        protected override async Task SerializeToStreamAsync(Stream stream, TransportContext context)
        {
            var buffer = _bufferPool.Rent(64000); // Rent 64 KB buffer
            try
            {
                // Start measuring upload time
                Stopwatch uploadWatch = Stopwatch.StartNew();
                using var contentStream = await _originalContent.ReadAsStreamAsync(_token);
                long totalBytesRead = 0;
                int bytesRead;
                
                while ((bytesRead = await contentStream.ReadAsync(buffer.AsMemory(0, buffer.Length), _token)) > 0)
                {
                    await stream.WriteAsync(buffer, 0, bytesRead, _token);
                    totalBytesRead += bytesRead;
                    _progress.Report(bytesRead);
                }
                
                // Stop timing and store in HttpRequestOptions
                uploadWatch.Stop();
                _httpRequestMessage.Options.Set(
                    new HttpRequestOptionsKey<double>("UploadTime"), 
                    uploadWatch.Elapsed.TotalMilliseconds
                );
            }
            finally
            {
                _bufferPool.Return(buffer); // Return the buffer to the pool
            }
        }

        protected override bool TryComputeLength(out long length)
        {
            if (_originalContent.Headers.ContentLength.HasValue)
            {
                length = _originalContent.Headers.ContentLength.Value;
                return true;
            }

            length = -1;
            return false;
        }
    }
}
