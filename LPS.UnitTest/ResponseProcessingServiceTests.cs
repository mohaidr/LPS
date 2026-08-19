using System.Net;
using System.Text;
using LPS.Domain;
using LPS.Domain.Common.Interfaces;
using LPS.Domain.LPSRequest.LPSHttpRequest;
using LPS.Infrastructure.Caching;
using LPS.Infrastructure.Common.Interfaces;
using LPS.Infrastructure.LPSClients.CachService;
using LPS.Infrastructure.LPSClients.ResponseService;
using LPS.Infrastructure.LPSClients.SampleResponseServices;
using LPS.Infrastructure.LPSClients.URLServices;
using LPS.Infrastructure.Monitoring.Hosts;
using Microsoft.Extensions.Caching.Memory;
using Moq;
using Xunit;

namespace LPS.UnitTest
{
    public class ResponseProcessingServiceTests
    {
        [Fact]
        public async Task ProcessResponseAsync_IsolatesCapturedContentBySession()
        {
            var requestId = Guid.NewGuid();
            var logger = new Mock<ILogger>();
            var operationIdProvider = new Mock<IRuntimeOperationIdProvider>();
            operationIdProvider.SetupGet(provider => provider.OperationId).Returns("test");

            var request = new HttpRequest(new HttpRequest.SetupCommand
            {
                Id = requestId,
                Url = new URL("https://example.com/api/carts"),
                HttpMethod = "POST",
                IsValid = true
            }, logger.Object, operationIdProvider.Object);

            using var memoryCache = new MemoryCache(new MemoryCacheOptions { SizeLimit = 100 });
            var cache = new MemoryCacheService<string>(memoryCache);
            var metricsService = new Mock<IMetricsService>();
            metricsService
                .Setup(service => service.TryUpdateDataReceivedAsync(requestId, It.IsAny<double>(), It.IsAny<CancellationToken>()))
                .Returns(new ValueTask<bool>(true));
            var hostMetricsService = new Mock<IHostMetricsService>();
            hostMetricsService
                .Setup(service => service.UpdateDataReceivedAsync(It.IsAny<HostKey>(), It.IsAny<double>(), It.IsAny<CancellationToken>()))
                .Returns(ValueTask.CompletedTask);
            var service = new ResponseProcessingService(
                cache,
                logger.Object,
                operationIdProvider.Object,
                Mock.Of<IResponsePersistenceFactory>(),
                metricsService.Object,
                Mock.Of<IUrlSanitizationService>(),
                hostMetricsService.Object);
            var hostKey = HostKey.From(new Uri("https://example.com/api/carts"));

            using var firstResponse = CreateJsonResponse("{\"id\":\"cart-1\"}");
            using var secondResponse = CreateJsonResponse("{\"id\":\"cart-2\"}");

            var firstResult = await service.ProcessResponseAsync(firstResponse, request, true, "session-1", hostKey, CancellationToken.None);
            var secondResult = await service.ProcessResponseAsync(secondResponse, request, true, "session-2", hostKey, CancellationToken.None);

            Assert.Equal(HttpStatusCode.OK, firstResult.command.StatusCode);
            Assert.Equal(HttpStatusCode.OK, secondResult.command.StatusCode);

            var firstContent = await cache.GetItemAsync($"{CachePrefixes.Content}{request.Id}-session-1");
            var secondContent = await cache.GetItemAsync($"{CachePrefixes.Content}{request.Id}-session-2");

            Assert.Equal("{\"id\":\"cart-1\"}", firstContent);
            Assert.Equal("{\"id\":\"cart-2\"}", secondContent);
        }

        private static HttpResponseMessage CreateJsonResponse(string content)
        {
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(content, Encoding.UTF8, "application/json")
            };
        }
    }
}
