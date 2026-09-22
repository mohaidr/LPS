using LPS.UI.Core.Host;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Console;
using Microsoft.Extensions.Options;
using Moq;
using Spectre.Console;

namespace LPS.UnitTest
{
    public class SpectreConsoleFormatterTests
    {
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void Write_PreservesLogDetailsAndUsesSpectre(bool singleLine)
        {
            using var output = new StringWriter();
            using var rawOutput = new StringWriter();
            var console = CreateConsole(output);
            var settings = new SimpleConsoleFormatterOptions
            {
                SingleLine = singleLine,
                IncludeScopes = true,
                ColorBehavior = LoggerColorBehavior.Disabled,
                TimestampFormat = "'timestamp '",
                UseUtcTimestamp = true
            };
            var options = Mock.Of<IOptionsMonitor<SimpleConsoleFormatterOptions>>(monitor => monitor.CurrentValue == settings);
            var formatter = new SpectreConsoleFormatter(new LiveConsoleOutput(console), options);
            var scopes = new LoggerExternalScopeProvider();
            using var scope = scopes.Push("test scope");
            var entry = new LogEntry<string>(LogLevel.Warning, "Dispatcher", new EventId(7), "[literal]\nsecond line",
                new InvalidOperationException("drain failed"), (state, exception) => state);

            formatter.Write(entry, scopes, rawOutput);

            var rendered = output.ToString();
            Assert.StartsWith("timestamp warn: Dispatcher[7]", rendered);
            Assert.Contains("=> test scope", rendered);
            Assert.Contains("[literal]", rendered);
            Assert.Contains("second line", rendered);
            Assert.Contains("InvalidOperationException: drain failed", rendered);
            Assert.Equal(string.Empty, rawOutput.ToString());
            if (singleLine)
            {
                Assert.DoesNotContain('\n', rendered.TrimEnd());
            }
        }

        [Fact]
        public async Task ConsoleLogger_PrintsShutdownMessagesThroughLiveDisplay()
        {
            using var output = new StringWriter();
            var console = CreateConsole(output, true);
            using var services = new ServiceCollection()
                .AddSingleton(console)
                .AddSingleton<ILiveConsoleOutput, LiveConsoleOutput>()
                .AddLogging(logging => logging.AddConsole(options => options.FormatterName = "spectre")
                    .AddConsoleFormatter<SpectreConsoleFormatter, SimpleConsoleFormatterOptions>())
                .BuildServiceProvider();
            var liveOutput = services.GetRequiredService<ILiveConsoleOutput>();
            var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("Dispatcher");

            liveOutput.BeginLiveDisplay();
            try
            {
                await console.Live(new Text("FINALIZING")).AutoClear(false).StartAsync(async context =>
                {
                    context.Refresh();
                    var beforeLogging = output.GetStringBuilder().Length;
                    await Task.Run(() =>
                    {
                        logger.LogInformation("CumulativeDispatcher stopped.");
                        logger.LogInformation("WindowedMetricsDispatcher stopped.");
                    });
                    Assert.Equal(beforeLogging, output.GetStringBuilder().Length);
                    liveOutput.FlushPendingLogs();
                    context.UpdateTarget(new Text("RUN COMPLETE"));
                    logger.LogInformation("Queued before display exit.");
                });
            }
            finally
            {
                liveOutput.EndLiveDisplay();
            }

            logger.LogInformation("Normal output restored.");
            var rendered = output.ToString();
            Assert.Contains("CumulativeDispatcher stopped.", rendered);
            Assert.Contains("WindowedMetricsDispatcher stopped.", rendered);
            Assert.Contains("Queued before display exit.", rendered);
            Assert.Contains("Normal output restored.", rendered);
            Assert.True(rendered.IndexOf("WindowedMetricsDispatcher stopped.") < rendered.LastIndexOf("RUN COMPLETE"));
        }

        [Fact]
        public async Task ConsoleLogger_BackgroundShutdownLogsDoNotBlockLiveRefresh()
        {
            var rendering = Task.Run(async () =>
            {
                using var output = new StringWriter();
                var console = CreateConsole(output, true);
                var liveOutput = new LiveConsoleOutput(console);
                var formatter = new SpectreConsoleFormatter(liveOutput,
                    Mock.Of<IOptionsMonitor<SimpleConsoleFormatterOptions>>(
                        monitor => monitor.CurrentValue == new SimpleConsoleFormatterOptions()));
                using var factory = LoggerFactory.Create(logging =>
                {
                    logging.Services.AddSingleton<ConsoleFormatter>(formatter);
                    logging.AddConsole(options => options.FormatterName = "spectre");
                });
                var logger = factory.CreateLogger("Dispatcher");

                liveOutput.BeginLiveDisplay();
                try
                {
                    await console.Live(new Text("FINALIZING")).AutoClear(false).StartAsync(async context =>
                    {
                        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                        var logging = Task.Run(() =>
                        {
                            started.SetResult();
                            for (var message = 0; message < 1000; message++)
                            {
                                logger.LogInformation("Dispatcher stopping {Message}", message);
                            }
                        });
                        await started.Task;
                        for (var frame = 0; frame < 1000; frame++)
                        {
                            liveOutput.FlushPendingLogs();
                            context.UpdateTarget(new Text("FINALIZING"));
                        }
                        await logging;
                        liveOutput.FlushPendingLogs();
                        context.UpdateTarget(new Text("RUN COMPLETE"));
                    });
                }
                finally
                {
                    liveOutput.EndLiveDisplay();
                }

                Assert.Contains("Dispatcher stopping 999", output.ToString());
                Assert.Contains("RUN COMPLETE", output.ToString());
            });

            await rendering.WaitAsync(TimeSpan.FromSeconds(5));
        }

        private static IAnsiConsole CreateConsole(TextWriter writer, bool interactive = false)
        {
            var console = AnsiConsole.Create(new AnsiConsoleSettings
            {
                Ansi = interactive ? AnsiSupport.Yes : AnsiSupport.No,
                Interactive = interactive ? InteractionSupport.Yes : InteractionSupport.No,
                ColorSystem = ColorSystemSupport.NoColors,
                Out = new AnsiConsoleOutput(writer)
            });
            console.Profile.Width = 200;
            return console;
        }
    }
}