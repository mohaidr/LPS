using LPS.UI.Core.Host;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Console;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Configuration;
using Moq;
using Spectre.Console;

namespace LPS.UnitTest
{
    public class SpectreConsoleFormatterTests
    {
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void Format_PreservesLogDetailsWithoutWritingOutput(bool singleLine)
        {
            using var output = new StringWriter();
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
            var formatter = new SpectreConsoleFormatter(options);
            var scopes = new LoggerExternalScopeProvider();
            using var scope = scopes.Push("test scope");
            var entry = new LogEntry<string>(LogLevel.Warning, "Dispatcher", new EventId(7), "[literal]\nsecond line",
                new InvalidOperationException("drain failed"), (state, exception) => state);

            var message = formatter.Format(entry, scopes);
            Assert.Equal(string.Empty, output.ToString());
            Assert.NotNull(message);
            console.Write(message);

            var rendered = output.ToString();
            Assert.StartsWith("timestamp warn: Dispatcher[7]", rendered);
            Assert.Contains("=> test scope", rendered);
            Assert.Contains("[literal]", rendered);
            Assert.Contains("second line", rendered);
            Assert.Contains("InvalidOperationException: drain failed", rendered);
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
                .AddLogging(logging => logging.AddLiveConsole())
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
                var liveOutput = new LiveConsoleOutput(console, console);
                using var factory = LoggerFactory.Create(logging =>
                {
                    logging.Services.AddSingleton<ILiveConsoleOutput>(liveOutput);
                    logging.AddLiveConsole();
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

        [Theory]
        [InlineData(false, false)]
        [InlineData(false, true)]
        [InlineData(true, false)]
        [InlineData(true, true)]
        public void ConsoleLogger_PreservesStreamsAndFlushesTerminalErrorsAfterDisplay(bool live, bool terminalError)
        {
            using var stdout = new StringWriter();
            using var stderr = new StringWriter();
            var liveOutput = new LiveConsoleOutput(CreateConsole(stdout, live), CreateConsole(stderr, terminalError));
            using var services = new ServiceCollection()
                .AddSingleton<ILiveConsoleOutput>(liveOutput)
                .AddLogging(logging =>
                {
                    logging.AddConsole(options => options.LogToStandardErrorThreshold = LogLevel.Warning);
                    logging.AddLiveConsole();
                    logging.AddLiveConsole();
                })
                .BuildServiceProvider();
            Assert.IsType<SpectreConsoleLoggerProvider>(Assert.Single(services.GetServices<ILoggerProvider>()));
            var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("Streams");

            if (live) liveOutput.BeginLiveDisplay();
            try
            {
                logger.LogInformation("stdout-marker");
                logger.LogWarning("stderr-marker");
                liveOutput.FlushPendingLogs();
                Assert.Contains("stdout-marker", stdout.ToString());
                Assert.DoesNotContain("stderr-marker", stdout.ToString());
                Assert.Equal(!live || !terminalError, stderr.ToString().Contains("stderr-marker"));
            }
            finally
            {
                if (live) liveOutput.EndLiveDisplay();
            }

            Assert.Contains("stderr-marker", stderr.ToString());
            Assert.DoesNotContain("stdout-marker", stderr.ToString());
            Assert.Equal(1, stdout.ToString().Split("stdout-marker").Length - 1);
            Assert.Equal(1, stderr.ToString().Split("stderr-marker").Length - 1);
        }

        [Fact]
        public void ConsoleLogger_PreservesFiltersScopesAndReloadableConsoleOptions()
        {
            using var stdout = new StringWriter();
            using var stderr = new StringWriter();
            var liveOutput = new LiveConsoleOutput(CreateConsole(stdout), CreateConsole(stderr));
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Logging:Console:LogLevel:Default"] = "Warning",
                ["Logging:Console:LogToStandardErrorThreshold"] = "Warning",
                ["Logging:Console:FormatterOptions:IncludeScopes"] = "true",
                ["Logging:Console:FormatterOptions:SingleLine"] = "true"
            }).Build();
            using var factory = LoggerFactory.Create(logging =>
            {
                logging.Services.AddSingleton<ILiveConsoleOutput>(liveOutput);
                logging.AddConfiguration(configuration.GetSection("Logging"));
                logging.AddLiveConsole();
            });
            var logger = factory.CreateLogger("Options");
            using var scope = logger.BeginScope("scope-marker");

            logger.LogInformation("filtered-marker");
            logger.LogWarning("first-marker");
            Assert.Equal(string.Empty, stdout.ToString());
            Assert.Contains("scope-marker", stderr.ToString());
            Assert.DoesNotContain('\n', stderr.ToString().TrimEnd());
            Assert.DoesNotContain("filtered-marker", stderr.ToString());

            configuration["Logging:Console:LogToStandardErrorThreshold"] = "Error";
            configuration["Logging:Console:FormatterOptions:SingleLine"] = "false";
            configuration.Reload();
            logger.LogWarning("second-marker");
            Assert.Contains("second-marker", stdout.ToString());
            Assert.Contains('\n', stdout.ToString().TrimEnd());
            Assert.DoesNotContain("second-marker", stderr.ToString());
        }

        [Fact]
        public void LiveOutput_FlushDoesNotChaseNewlyQueuedMessages()
        {
            var console = new Mock<IAnsiConsole>();
            var liveOutput = new LiveConsoleOutput(console.Object, console.Object);
            var writes = 0;
            console.Setup(current => current.Write(It.IsAny<Spectre.Console.Rendering.IRenderable>()))
                .Callback(() =>
                {
                    if (++writes == 1) liveOutput.Write(new Text("next-frame"));
                });

            liveOutput.BeginLiveDisplay();
            liveOutput.Write(new Text("current-frame"));
            liveOutput.FlushPendingLogs();
            Assert.Equal(1, writes);
            liveOutput.EndLiveDisplay();
            Assert.Equal(2, writes);
        }

        [Fact]
        public void LiveOutput_BoundsFrameWorkAndDrainsTheEntireBacklogOnExit()
        {
            using var writer = new StringWriter();
            var console = CreateConsole(writer);
            var liveOutput = new LiveConsoleOutput(console, console);
            liveOutput.BeginLiveDisplay();
            for (var index = 0; index < 1000; index++)
                liveOutput.Write(new Text($"message-{index}\n"));

            liveOutput.FlushPendingLogs();

            Assert.Contains($"message-0{Environment.NewLine}", writer.ToString());
            Assert.DoesNotContain($"message-999{Environment.NewLine}", writer.ToString());
            liveOutput.EndLiveDisplay();
            Assert.Equal(1000, writer.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries).Length);
            Assert.Contains($"message-999{Environment.NewLine}", writer.ToString());
        }

        [Fact]
        public void Registration_PreservesOtherLoggingProviders()
        {
            var other = new Mock<ILoggerProvider>();
            other.Setup(provider => provider.CreateLogger(It.IsAny<string>())).Returns(Mock.Of<ILogger>());
            using var services = new ServiceCollection()
                .AddLogging(logging =>
                {
                    logging.AddProvider(other.Object);
                    logging.AddConsole();
                    logging.AddLiveConsole();
                })
                .BuildServiceProvider();

            var providers = services.GetServices<ILoggerProvider>().ToArray();
            Assert.Equal(2, providers.Length);
            Assert.Contains(other.Object, providers);
            Assert.Single(providers.OfType<SpectreConsoleLoggerProvider>());
            Assert.Empty(providers.OfType<ConsoleLoggerProvider>());
        }

        private static IAnsiConsole CreateConsole(TextWriter writer, bool interactive = false)
        {
            var console = AnsiConsole.Create(new AnsiConsoleSettings
            {
                Ansi = interactive ? AnsiSupport.Yes : AnsiSupport.No,
                Interactive = interactive ? InteractionSupport.Yes : InteractionSupport.No,
                ColorSystem = ColorSystemSupport.NoColors,
                Out = interactive
                    ? Mock.Of<IAnsiConsoleOutput>(current => current.IsTerminal && current.Writer == writer && current.Width == 200 && current.Height == 40)
                    : new AnsiConsoleOutput(writer)
            });
            console.Profile.Width = 200;
            return console;
        }
    }
}