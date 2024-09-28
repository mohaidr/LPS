// ResponseProcessingService.cs
using LPS.Domain.Common;
using LPS.Domain.Common.Interfaces;
using LPS.Infrastructure.Caching;
using LPS.Infrastructure.LPSClients.URLServices;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace LPS.Infrastructure.LPSClients.SampleResponseServices
{
    public class ResponseProcessingService : IResponseProcessingService
    {
        private readonly ICacheService<string> _memoryCache;
        private readonly ILogger _logger;
        private readonly IRuntimeOperationIdProvider _runtimeOperationIdProvider;

        public ResponseProcessingService(
            IRuntimeOperationIdProvider runtimeOperationIdProvider,
            ILogger logger,
            ICacheService<string> memoryCache)
        {
            _logger = logger;
            _runtimeOperationIdProvider = runtimeOperationIdProvider;
            _memoryCache = memoryCache;
        }

        public async Task<IResponseProcessor> CreateResponseProcessorAsync(string url,MimeType responseType, bool saveResponse, CancellationToken token)
        {

            if (!saveResponse)
            {
                // Return a no-op processor
                return new NoOpResponseProcessor();
            }

            // Create a processor that will handle response saving
            var processor = new FileResponseProcessor(
                url,
                _memoryCache,
                _logger,
                _runtimeOperationIdProvider);

            await processor.InitializeAsync(responseType.ToFileExtension(), token);
            return processor;
        }
    }
}
