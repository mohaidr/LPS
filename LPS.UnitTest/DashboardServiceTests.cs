using LPS.UI.Core.Host;
using LPS.Domain.Common.Interfaces;
using LPS.Infrastructure.Nodes;
using LPS.UI.Common.Options;
using Microsoft.Extensions.Options;
using Moq;
using LPS.Infrastructure.Distributed;

namespace LPS.UnitTest
{
    public class DashboardServiceTests
    {
        [Theory]
        [InlineData(false, "192.168.1.10", "http://192.168.1.10:8110")]
        [InlineData(true, "https://master.example:5161", "http://127.0.0.1:8110")]
        public void DashboardUrl_UsesLoopbackOnlyForIsolatedRuns(bool distributed, string address, string expected)
        {
            var cluster = new ClusterConfiguration(address, 5161, false, 1);
            var run = distributed ? new ClusterRunSettings
            {
                RunId = Guid.NewGuid().ToString(), NodeName = "master", Role = NodeType.Master,
                RunDirectory = "unused", PlanPath = "unused", NodeAddress = address, MasterAddress = address, Token = new string('x', 64)
            } : null;
            var service = new DashboardService(Mock.Of<ILogger>(), Mock.Of<IRuntimeOperationIdProvider>(),
                Options.Create(new DashboardConfigurationOptions { Port = 8110 }), cluster, Mock.Of<IFinalizationDisplay>(), run);

            Assert.Equal(expected, service.Url);
        }

        [Fact]
        public async Task EnsureDashboardUpdateBeforeExitAsync_AllowsTwoEndpointPollingIntervals()
        {
            var display = new Mock<IFinalizationDisplay>();
            var service = new DashboardService(
                Mock.Of<ILogger>(),
                Mock.Of<IRuntimeOperationIdProvider>(),
                Options.Create(new DashboardConfigurationOptions { RefreshRate = 1 }),
                Mock.Of<IClusterConfiguration>(),
                display.Object);

            await service.EnsureDashboardUpdateBeforeExitAsync();

            display.Verify(current => current.Start(TimeSpan.FromSeconds(10)), Times.Once);
        }
    }
}