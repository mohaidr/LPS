// ResponseProcessingService.cs
using LPS.Domain;
using LPS.Domain.Common;
using LPS.Domain.Common.Interfaces;
using LPS.Infrastructure.Caching;
using LPS.Infrastructure.LPSClients.URLServices;
using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace LPS.Infrastructure.LPSClients.SampleResponseServices
{
    public class ResponsePersistenceFactory : IResponsePersistenceFactory
    {
        private readonly ICacheService<string> _memoryCache;
        private readonly ILogger _logger;
        private readonly IRuntimeOperationIdProvider _runtimeOperationIdProvider;
        readonly IUrlSanitizationService _urlSanitizationService;

        public ResponsePersistenceFactory(
            IRuntimeOperationIdProvider runtimeOperationIdProvider,
            ILogger logger,
            ICacheService<string> memoryCache,
            IUrlSanitizationService urlSanitizationService)
        {
            _logger = logger;
            _runtimeOperationIdProvider = runtimeOperationIdProvider;
            _memoryCache = memoryCache;
            _urlSanitizationService = urlSanitizationService;
        }

        public async Task<IResponsePersistence> CreateAsync(string? destination, PersistenceType pType = PersistenceType.File, CancellationToken token = default)
        {

            if (pType == PersistenceType.Memory)
            {
                // Return a no-op persistence
                throw new NotImplementedException(nameof(pType.Memory));
            }

            // Create a processor that will handle response saving
            var persistence = new FileResponsePersistence(
                _memoryCache,
                _logger,
                _runtimeOperationIdProvider);

            await persistence.InitializeAsync(destination, token);
            return persistence;
        }
    }
}
