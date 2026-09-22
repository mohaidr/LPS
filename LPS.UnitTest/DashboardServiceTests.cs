using LPS.UI.Core.Host;
using LPS.Domain.Common.Interfaces;
using LPS.Infrastructure.Nodes;
using LPS.UI.Common.Options;
using Microsoft.Extensions.Options;
using Moq;

namespace LPS.UnitTest
{
    public class DashboardServiceTests
    {
        [Fact]
        public async Task EnsureDashboardUpdateBeforeExitAsync_StartsFinalization()
        {
            var display = new Mock<IFinalizationDisplay>();
            var service = new DashboardService(
                Mock.Of<ILogger>(),
                Mock.Of<IRuntimeOperationIdProvider>(),
                Options.Create(new DashboardConfigurationOptions { RefreshRate = 0 }),
                Mock.Of<IClusterConfiguration>(),
                display.Object);

            await service.EnsureDashboardUpdateBeforeExitAsync();

            display.Verify(current => current.Start(TimeSpan.Zero), Times.Once);
        }
    }
}