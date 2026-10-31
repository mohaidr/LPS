using LPS.Domain.Common.Interfaces;
using LPS.Infrastructure.Monitoring.Cumulative;
using LPS.Infrastructure.Monitoring.Windowed;
using LPS.UI.Core.Services;
using Moq;

namespace LPS.UnitTest
{
    [CollectionDefinition("Console error routing", DisableParallelization = true)]
    public class ConsoleErrorRoutingCollection
    {
    }

    [Collection("Console error routing")]
    public class ConsoleErrorRoutingTests
    {
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task ConfigurationFailure_WritesToStandardError(bool saving)
        {
            var output = await CaptureConsoleAsync(() =>
            {
                if (saving)
                    ConfigurationService.SaveConfiguration("unused.unsupported", "content");
                else
                    Assert.Null(ConfigurationService.FetchConfiguration<string>("unused.unsupported", Mock.Of<IPlaceholderResolverService>()));
                return Task.CompletedTask;
            });

            Assert.Empty(output.StandardOutput);
            Assert.Contains(saving ? "Error saving configuration:" : "Error fetching configuration:", output.StandardError);
            Assert.Contains("Unsupported file format", output.StandardError);
        }

        [Theory]
        [InlineData(false, false)]
        [InlineData(false, true)]
        [InlineData(true, false)]
        [InlineData(true, true)]
        public async Task MetricsShutdownFailure_WritesToStandardError(bool windowed, bool asynchronous)
        {
            var output = await CaptureConsoleAsync(async () =>
            {
                if (windowed)
                {
                    using var coordinator = new WindowedMetricsCoordinator(TimeSpan.FromHours(1));
                    coordinator.OnWindowClosed += () => Task.FromException(new InvalidOperationException("Final snapshot failed."));
                    coordinator.Start();
                    if (asynchronous)
                        await coordinator.StopAsync(CancellationToken.None);
                    else
                        coordinator.Stop();
                }
                else
                {
                    using var coordinator = new CumulativeMetricsCoordinator(TimeSpan.FromHours(1));
                    coordinator.OnPushInterval += () => Task.FromException(new InvalidOperationException("Final snapshot failed."));
                    coordinator.Start();
                    if (asynchronous)
                        await coordinator.StopAsync(CancellationToken.None);
                    else
                        coordinator.Stop();
                }
            });

            Assert.Empty(output.StandardOutput);
            Assert.Contains("InvalidOperationException: Final snapshot failed.", output.StandardError);
        }

        private static async Task<(string StandardOutput, string StandardError)> CaptureConsoleAsync(Func<Task> action)
        {
            var originalOutput = Console.Out;
            var originalError = Console.Error;
            using var output = new StringWriter();
            using var error = new StringWriter();
            try
            {
                Console.SetOut(output);
                Console.SetError(error);
                await action();
                return (output.ToString(), error.ToString());
            }
            finally
            {
                Console.SetOut(originalOutput);
                Console.SetError(originalError);
            }
        }
    }
}
