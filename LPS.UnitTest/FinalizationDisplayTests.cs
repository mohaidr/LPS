using LPS.Domain.Common.Interfaces;
using LPS.Infrastructure.Nodes;
using LPS.UI.Common.Options;
using LPS.UI.Core.Host;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Moq;
using Spectre.Console;

namespace LPS.UnitTest
{
    public class FinalizationDisplayTests
    {
        [Fact]
        public async Task ShowUntilShutdownAsync_WaitsAfterDashboardRefresh()
        {
            var display = CreateDisplay();
            var dashboard = new DashboardService(
                Mock.Of<ILogger>(),
                Mock.Of<IRuntimeOperationIdProvider>(),
                Options.Create(new DashboardConfigurationOptions { RefreshRate = 0 }),
                Mock.Of<IClusterConfiguration>(),
                display);
            var shutdown = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var rendering = display.ShowUntilShutdownAsync(shutdown.Task);

            await dashboard.EnsureDashboardUpdateBeforeExitAsync();

            Assert.False(rendering.IsCompleted);
            shutdown.SetResult();
            await rendering.WaitAsync(TimeSpan.FromSeconds(2));
        }

        [Fact]
        public async Task ShowUntilShutdownAsync_WaitsForHostedServicesToStop()
        {
            var stopping = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var dispatcher = new Mock<IHostedService>();
            dispatcher.Setup(service => service.StartAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            dispatcher.Setup(service => service.StopAsync(It.IsAny<CancellationToken>()))
                .Returns(async (CancellationToken token) =>
                {
                    stopping.TrySetResult();
                    await release.Task.WaitAsync(token);
                });
            using var host = new HostBuilder()
                .ConfigureServices((context, services) => services.AddSingleton(dispatcher.Object))
                .Build();
            await host.StartAsync();
            var display = CreateDisplay();
            var rendering = display.ShowUntilShutdownAsync(host.WaitForShutdownAsync());
            display.Start(TimeSpan.Zero);

            host.Services.GetRequiredService<IHostApplicationLifetime>().StopApplication();
            try
            {
                await stopping.Task.WaitAsync(TimeSpan.FromSeconds(2));
                Assert.False(rendering.IsCompleted);
            }
            finally
            {
                release.TrySetResult();
            }

            await rendering.WaitAsync(TimeSpan.FromSeconds(2));
        }

        [Fact]
        public async Task ShowUntilShutdownAsync_DoesNotWaitWhenNoTestRan()
        {
            await CreateDisplay().ShowUntilShutdownAsync(Task.CompletedTask)
                .WaitAsync(TimeSpan.FromSeconds(2));
        }

        [Fact]
        public async Task ShowUntilShutdownAsync_PropagatesShutdownFailure()
        {
            var display = CreateDisplay();
            display.Start(TimeSpan.Zero);
            var failure = new InvalidOperationException("Shutdown failed");

            var actual = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                display.ShowUntilShutdownAsync(Task.FromException(failure)));

            Assert.Same(failure, actual);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task ShowUntilShutdownAsync_RestoresLogOutputAfterInteractiveDisplay(bool shutdownFails)
        {
            using var writer = new StringWriter();
            var terminal = Mock.Of<IAnsiConsoleOutput>(current =>
                current.IsTerminal && current.Writer == writer && current.Width == 80 && current.Height == 30);
            var console = AnsiConsole.Create(new AnsiConsoleSettings
            {
                Ansi = AnsiSupport.Yes,
                Interactive = InteractionSupport.Yes,
                Out = terminal
            });
            var output = new LiveConsoleOutput(console, console);
            var display = new FinalizationDisplay(console, output);
            display.Start(TimeSpan.Zero);
            var shutdown = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var rendering = display.ShowUntilShutdownAsync(shutdown.Task);
            output.Write(new Text("Buffered error retained."), standardError: true);

            if (shutdownFails)
            {
                var failure = new InvalidOperationException("Shutdown failed");
                shutdown.SetException(failure);
                var actual = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                    rendering.WaitAsync(TimeSpan.FromSeconds(2)));
                Assert.Same(failure, actual);
                Assert.DoesNotContain("RUN COMPLETE", writer.ToString());
            }
            else
            {
                shutdown.SetResult();
                await rendering.WaitAsync(TimeSpan.FromSeconds(2));
                Assert.Contains("RUN COMPLETE", writer.ToString());
            }

            Assert.Contains("Buffered error retained.", writer.ToString());
            output.Write(new Text("Normal output restored."));
            Assert.Contains("Normal output restored.", writer.ToString());
        }

        [Fact]
        public async Task ShowUntilShutdownAsync_WritesPlainTextWhenOutputIsRedirected()
        {
            using var writer = new StringWriter();
            var console = AnsiConsole.Create(new AnsiConsoleSettings
            {
                Ansi = AnsiSupport.Yes,
                Interactive = InteractionSupport.Yes,
                Out = new AnsiConsoleOutput(writer)
            });
            var display = new FinalizationDisplay(console, new LiveConsoleOutput(console, console));
            display.Start(TimeSpan.Zero);

            await display.ShowUntilShutdownAsync(Task.CompletedTask);

            Assert.Equal($"Finalization complete.{Environment.NewLine}", writer.ToString());
        }

        private static FinalizationDisplay CreateDisplay()
        {
            var console = AnsiConsole.Create(new AnsiConsoleSettings
            {
                Ansi = AnsiSupport.No,
                Interactive = InteractionSupport.No,
                Out = new AnsiConsoleOutput(TextWriter.Null)
            });
            return new FinalizationDisplay(console, new LiveConsoleOutput(console, console));
        }
    }
}