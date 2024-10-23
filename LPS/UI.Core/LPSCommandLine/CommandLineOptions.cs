using LPS.Domain;
using System;
using System.Collections.Generic;
using System.Linq;
using System.CommandLine;
using static LPS.Domain.HttpRun;
using LPS.Domain.Common.Interfaces;
using System.Reflection;
using LPS.Infrastructure.Watchdog;
using LPS.Domain.Domain.Common.Enums;

namespace LPS.UI.Core.LPSCommandLine
{
    public static class CommandLineOptions
    {
        static CommandLineOptions()
        {
            // No changes needed here
        }

        // Helper method to add case variations
        private static void AddCaseInsensitiveAliases(Option option, params string[] aliases)
        {
            foreach (var alias in aliases)
            {
                option.AddAlias(alias);
                var lowerAlias = alias.ToLowerInvariant();
                if (alias != lowerAlias)
                {
                    option.AddAlias(lowerAlias);
                }
                var upperAlias = alias.ToUpperInvariant();
                if (alias != upperAlias)
                {
                    option.AddAlias(upperAlias);
                }
                if (alias.StartsWith("--") && alias.Length > 2)
                {
                    var name = alias.Substring(2);
                    var camelCaseName = char.ToLowerInvariant(name[0]) + name.Substring(1);
                    var camelCaseAlias = "--" + camelCaseName;
                    if (alias != camelCaseAlias)
                    {
                        option.AddAlias(camelCaseAlias);
                    }
                }
            }
        }

        public static class LPSCommandOptions
        {
            static LPSCommandOptions()
            {
                // Shortcut aliases
                TestNameOption.AddAlias("-tn");
                DelayClientCreation.AddAlias("-dcc");
                NumberOfClientsOption.AddAlias("-nc");
                ArrivalDelayOption.AddAlias("-ad");
                RunInParallel.AddAlias("-rip");
                RunNameOption.AddAlias("-rn");
                RequestCountOption.AddAlias("-rc");
                Duration.AddAlias("-d");
                BatchSize.AddAlias("-bs");
                CoolDownTime.AddAlias("-cdt");
                HttpMethodOption.AddAlias("-hm");
                HttpVersionOption.AddAlias("-hv");
                UrlOption.AddAlias("-u");
                HeaderOption.AddAlias("-h");
                PayloadOption.AddAlias("-p");
                IterationModeOption.AddAlias("-im");
                MaximizeThroughputOption.AddAlias("-mt");
                DownloadHtmlEmbeddedResources.AddAlias("-dhtmler");
                SaveResponse.AddAlias("-sr");
                SupportH2C.AddAlias("-sh2c");

                // Add case-insensitive aliases
                AddCaseInsensitiveAliases(TestNameOption, "--testname");
                AddCaseInsensitiveAliases(DelayClientCreation, "--delayclientcreation");
                AddCaseInsensitiveAliases(NumberOfClientsOption, "--numberofclients");
                AddCaseInsensitiveAliases(ArrivalDelayOption, "--arrivaldelay");
                AddCaseInsensitiveAliases(RunInParallel, "--runinparallel");
                AddCaseInsensitiveAliases(RunNameOption, "--runname");
                AddCaseInsensitiveAliases(RequestCountOption, "--requestcount");
                AddCaseInsensitiveAliases(Duration, "--duration");
                AddCaseInsensitiveAliases(BatchSize, "--batchsize");
                AddCaseInsensitiveAliases(CoolDownTime, "--cooldowntime");
                AddCaseInsensitiveAliases(HttpMethodOption, "--method", "--httpmethod");
                AddCaseInsensitiveAliases(HttpVersionOption, "--httpversion");
                AddCaseInsensitiveAliases(UrlOption, "--url");
                AddCaseInsensitiveAliases(HeaderOption, "--header");
                AddCaseInsensitiveAliases(PayloadOption, "--payload");
                AddCaseInsensitiveAliases(IterationModeOption, "--iterationmode");
                AddCaseInsensitiveAliases(MaximizeThroughputOption, "--maximizethroughput");
                AddCaseInsensitiveAliases(DownloadHtmlEmbeddedResources, "--downloadhtmlembeddedresources");
                AddCaseInsensitiveAliases(SaveResponse, "--saveresponse");
                AddCaseInsensitiveAliases(SupportH2C, "--supporth2c");
            }

            public static Option<string> TestNameOption { get; } = new Option<string>(
                "--testname", () => "Quick-Test-Plan", "Test name")
            {
                IsRequired = false,
                Arity = ArgumentArity.ExactlyOne
            };

            public static Option<int> NumberOfClientsOption { get; } = new Option<int>(
                "--numberofclients", () => 1, "Number of clients to execute the plan")
            {
                IsRequired = false
            };

            public static Option<int> ArrivalDelayOption { get; } = new Option<int>(
                "--arrivaldelay", () => 0, "Time in milliseconds to wait before a new client arrives")
            {
                IsRequired = false
            };

            public static Option<bool> DelayClientCreation { get; } = new Option<bool>(
                "--delayclientcreation", () => false, "Delay client creation until needed")
            {
                IsRequired = false
            };

            public static Option<bool> RunInParallel { get; } = new Option<bool>(
                "--runinparallel", () => true, "Execute your runs in parallel")
            {
                IsRequired = false
            };

            public static Option<string> UrlOption { get; } = new Option<string>(
                "--url", "URL")
            {
                IsRequired = true
            };

            public static Option<string> HttpMethodOption { get; } = new Option<string>(
                "--method", () => "GET", "HTTP method")
            {
                IsRequired = false
            };

            public static Option<IList<string>> HeaderOption { get; } = new Option<IList<string>>(
                "--header", "Header")
            {
                IsRequired = false,
                AllowMultipleArgumentsPerToken = true
            };

            public static Option<string> RunNameOption { get; } = new Option<string>(
                "--runname", () => "Quick-Http-Run", "Run name")
            {
                IsRequired = false
            };

            public static Option<IterationMode> IterationModeOption { get; } = new Option<IterationMode>(
                "--iterationmode", () => IterationMode.R, "Defines iteration mode")
            {
                IsRequired = false
            };

            public static Option<bool> MaximizeThroughputOption { get; } = new Option<bool>(
                "--maximizethroughput", () => false, "Maximize test throughput")
            {
                IsRequired = false
            };

            public static Option<int?> RequestCountOption { get; } = new Option<int?>(
                "--requestcount", () => null, "Number of requests")
            {
                IsRequired = false
            };

            public static Option<int?> Duration { get; } = new Option<int?>(
                "--duration", () => null, "Duration in seconds")
            {
                IsRequired = false
            };

            public static Option<int?> CoolDownTime { get; } = new Option<int?>(
                "--cooldowntime", () => null, "Cooldown time in milliseconds")
            {
                IsRequired = false
            };

            public static Option<int?> BatchSize { get; } = new Option<int?>(
                "--batchsize", () => null, "Batch size")
            {
                IsRequired = false
            };

            public static Option<string> HttpVersionOption { get; } = new Option<string>(
                "--httpversion", () => "2.0", "HTTP version")
            {
                IsRequired = false
            };

            public static Option<bool> DownloadHtmlEmbeddedResources { get; } = new Option<bool>(
                "--downloadhtmlembeddedresources", () => false, "Download HTML embedded resources")
            {
                IsRequired = false
            };

            public static Option<bool> SaveResponse { get; } = new Option<bool>(
                "--saveresponse", () => false, "Save HTTP response")
            {
                IsRequired = false
            };
            public static Option<bool> SupportH2C { get; } = new Option<bool>(
                "--supportH2C", () => false, "Enables support for HTTP/2 over clear text. If used with a non-HTTP/2 protocol, it will override the protocol setting and enforce HTTP/2.")
            {
                IsRequired = false
            };

            public static Option<string> PayloadOption { get; } = new Option<string>(
                "--payload", () => string.Empty, "Request payload")
            {
                IsRequired = false
            };
        }

        public static class LPSCreateCommandOptions
        {
            static LPSCreateCommandOptions()
            {
                // Shortcut aliases
                TestNameOption.AddAlias("-tn");
                DelayClientCreation.AddAlias("-dcc");
                NumberOfClientsOption.AddAlias("-nc");
                ArrivalDelayOption.AddAlias("-ad");
                RunInParallel.AddAlias("-rip");

                // Add case-insensitive aliases
                AddCaseInsensitiveAliases(TestNameOption, "--testname");
                AddCaseInsensitiveAliases(DelayClientCreation, "--delayclientcreation");
                AddCaseInsensitiveAliases(NumberOfClientsOption, "--numberofclients");
                AddCaseInsensitiveAliases(ArrivalDelayOption, "--arrivaldelay");
                AddCaseInsensitiveAliases(RunInParallel, "--runinparallel");
            }

            public static Option<string> TestNameOption { get; } = new Option<string>(
                "--testname", "Test name")
            {
                IsRequired = true,
                Arity = ArgumentArity.ExactlyOne
            };

            public static Option<int> NumberOfClientsOption { get; } = new Option<int>(
                "--numberofclients", "Number of clients to execute the plan")
            {
                IsRequired = true
            };

            public static Option<int> ArrivalDelayOption { get; } = new Option<int>(
                "--arrivaldelay", "Time in milliseconds to wait before a new client arrives")
            {
                IsRequired = true
            };

            public static Option<bool> DelayClientCreation { get; } = new Option<bool>(
                "--delayclientcreation", () => false, "Delay client creation until needed")
            {
                IsRequired = false
            };

            public static Option<bool> RunInParallel { get; } = new Option<bool>(
                "--runinparallel", () => true, "Execute your runs in parallel")
            {
                IsRequired = false
            };
        }

        public static class LPSAddCommandOptions
        {
            static LPSAddCommandOptions()
            {
                // Shortcut aliases
                TestNameOption.AddAlias("-tn");
                RunNameOption.AddAlias("-rn");
                RequestCountOption.AddAlias("-rc");
                Duration.AddAlias("-d");
                BatchSize.AddAlias("-bs");
                CoolDownTime.AddAlias("-cdt");
                HttpMethodOption.AddAlias("-hm");
                HttpVersionOption.AddAlias("-hv");
                UrlOption.AddAlias("-u");
                HeaderOption.AddAlias("-h");
                PayloadOption.AddAlias("-p");
                IterationModeOption.AddAlias("-im");
                MaximizeThroughputOption.AddAlias("-mt");
                DownloadHtmlEmbeddedResources.AddAlias("-dhtmler");
                SaveResponse.AddAlias("-sr");
                SupportH2C.AddAlias("-h2c");

                // Add case-insensitive aliases
                AddCaseInsensitiveAliases(TestNameOption, "--testname");
                AddCaseInsensitiveAliases(RunNameOption, "--runname");
                AddCaseInsensitiveAliases(RequestCountOption, "--requestcount");
                AddCaseInsensitiveAliases(Duration, "--duration");
                AddCaseInsensitiveAliases(BatchSize, "--batchsize");
                AddCaseInsensitiveAliases(CoolDownTime, "--cooldowntime");
                AddCaseInsensitiveAliases(HttpMethodOption, "--method", "--httpmethod");
                AddCaseInsensitiveAliases(HttpVersionOption, "--httpversion");
                AddCaseInsensitiveAliases(UrlOption, "--url");
                AddCaseInsensitiveAliases(HeaderOption, "--header");
                AddCaseInsensitiveAliases(PayloadOption, "--payload");
                AddCaseInsensitiveAliases(IterationModeOption, "--iterationmode");
                AddCaseInsensitiveAliases(MaximizeThroughputOption, "--maximizethroughput");
                AddCaseInsensitiveAliases(DownloadHtmlEmbeddedResources, "--downloadhtmlembeddedresources");
                AddCaseInsensitiveAliases(SaveResponse, "--saveresponse");
                AddCaseInsensitiveAliases(SupportH2C, "--supporth2c");
            }

            public static Option<string> TestNameOption { get; } = new Option<string>(
                "--testname", "Test name")
            {
                IsRequired = true,
                Arity = ArgumentArity.ExactlyOne
            };

            public static Option<string> RunNameOption { get; } = new Option<string>(
                "--runname", "Run name")
            {
                IsRequired = true
            };

            public static Option<IterationMode> IterationModeOption { get; } = new Option<IterationMode>(
                "--iterationmode", "Defines iteration mode")
            {
                IsRequired = false
            };

            public static Option<int?> RequestCountOption { get; } = new Option<int?>(
                "--requestcount", "Number of requests")
            {
                IsRequired = false
            };

            public static Option<bool> MaximizeThroughputOption { get; } = new Option<bool>(
                "--maximizethroughput", () => false, "Maximize test throughput")
            {
                IsRequired = false
            };

            public static Option<int?> Duration { get; } = new Option<int?>(
                "--duration", "Duration in seconds")
            {
                IsRequired = false
            };

            public static Option<int?> CoolDownTime { get; } = new Option<int?>(
                "--cooldowntime", "Cooldown time in milliseconds")
            {
                IsRequired = false
            };

            public static Option<int?> BatchSize { get; } = new Option<int?>(
                "--batchsize", "Batch size")
            {
                IsRequired = false
            };

            public static Option<string> HttpMethodOption { get; } = new Option<string>(
                "--method", "HTTP method")
            {
                IsRequired = true
            };

            public static Option<string> HttpVersionOption { get; } = new Option<string>(
                "--httpversion", () => "2.0", "HTTP version")
            {
                IsRequired = false
            };

            public static Option<string> UrlOption { get; } = new Option<string>(
                "--url", "URL")
            {
                IsRequired = true
            };

            public static Option<bool> DownloadHtmlEmbeddedResources { get; } = new Option<bool>(
                "--downloadhtmlembeddedresources", () => false, "Download HTML embedded resources")
            {
                IsRequired = false
            };

            public static Option<bool> SaveResponse { get; } = new Option<bool>(
                "--saveresponse", () => false, "Save HTTP response")
            {
                IsRequired = false
            };

            public static Option<bool> SupportH2C { get; } = new Option<bool>(
                "--supporth2c", () => false, "Enables support for HTTP/2 over clear text. If used with a non-HTTP/2 protocol, it will override the protocol setting and enforce HTTP/2.")
            {
                IsRequired = false
            };

            public static Option<IList<string>> HeaderOption { get; } = new Option<IList<string>>(
                "--header", "Header")
            {
                IsRequired = false,
                AllowMultipleArgumentsPerToken = true
            };

            public static Option<string> PayloadOption { get; } = new Option<string>(
                "--payload", "Request payload")
            {
                IsRequired = false
            };
        }

        public static class LPSRunCommandOptions
        {
            static LPSRunCommandOptions()
            {
                TestNameOption.AddAlias("-tn");

                // Add case-insensitive aliases
                AddCaseInsensitiveAliases(TestNameOption, "--testname");
            }

            public static Option<string> TestNameOption { get; } = new Option<string>(
                "--testname", "Test name")
            {
                IsRequired = true,
                Arity = ArgumentArity.ExactlyOne
            };
        }

        public static class LPSLoggerCommandOptions
        {
            static LPSLoggerCommandOptions()
            {
                // Shortcut aliases
                LogFilePathOption.AddAlias("-lfp");
                DisableFileLoggingOption.AddAlias("-dfl");
                EnableConsoleLoggingOption.AddAlias("-ecl");
                DisableConsoleErrorLoggingOption.AddAlias("-dcel");
                LoggingLevelOption.AddAlias("-ll");
                ConsoleLoggingLevelOption.AddAlias("-cll");

                // Add case-insensitive aliases
                AddCaseInsensitiveAliases(LogFilePathOption, "--logfilepath");
                AddCaseInsensitiveAliases(DisableFileLoggingOption, "--disablefilelogging");
                AddCaseInsensitiveAliases(EnableConsoleLoggingOption, "--enableconsolelogging");
                AddCaseInsensitiveAliases(DisableConsoleErrorLoggingOption, "--disableconsoleerrorlogging");
                AddCaseInsensitiveAliases(LoggingLevelOption, "--logginglevel");
                AddCaseInsensitiveAliases(ConsoleLoggingLevelOption, "--consolelogginglevel");
            }

            public static Option<string> LogFilePathOption { get; } = new Option<string>(
                "--logfilepath", "Path to log file")
            {
                IsRequired = false
            };

            public static Option<bool?> EnableConsoleLoggingOption { get; } = new Option<bool?>(
                "--enableconsolelogging", "Enable console logging")
            {
                IsRequired = false
            };

            public static Option<bool?> DisableConsoleErrorLoggingOption { get; } = new Option<bool?>(
                "--disableconsoleerrorlogging", "Disable console error logging")
            {
                IsRequired = false
            };

            public static Option<bool?> DisableFileLoggingOption { get; } = new Option<bool?>(
                "--disablefilelogging", "Disable file logging")
            {
                IsRequired = false
            };

            public static Option<LPSLoggingLevel?> LoggingLevelOption { get; } = new Option<LPSLoggingLevel?>(
                "--logginglevel", "Logging level")
            {
                IsRequired = false
            };

            public static Option<LPSLoggingLevel?> ConsoleLoggingLevelOption { get; } = new Option<LPSLoggingLevel?>(
                "--consolelogginglevel", "Console logging level")
            {
                IsRequired = false
            };
        }

        public static class LPSHttpClientCommandOptions
        {
            static LPSHttpClientCommandOptions()
            {
                // Shortcut aliases
                MaxConnectionsPerServerOption.AddAlias("-mcps");
                PoolConnectionLifetimeOption.AddAlias("-pclt");
                PoolConnectionIdleTimeoutOption.AddAlias("-pcit");
                ClientTimeoutOption.AddAlias("-cto");

                // Add case-insensitive aliases
                AddCaseInsensitiveAliases(MaxConnectionsPerServerOption, "--maxconnectionsperserver");
                AddCaseInsensitiveAliases(PoolConnectionLifetimeOption, "--poolconnectionlifetime");
                AddCaseInsensitiveAliases(PoolConnectionIdleTimeoutOption, "--poolconnectionidletimeout");
                AddCaseInsensitiveAliases(ClientTimeoutOption, "--clienttimeout");
            }

            public static Option<int?> MaxConnectionsPerServerOption { get; } = new Option<int?>(
                "--maxconnectionsperserver", "Max connections per server")
            {
                IsRequired = false
            };

            public static Option<int?> PoolConnectionLifetimeOption { get; } = new Option<int?>(
                "--poolconnectionlifetime", "Pooled connection lifetime in seconds")
            {
                IsRequired = false
            };

            public static Option<int?> PoolConnectionIdleTimeoutOption { get; } = new Option<int?>(
                "--poolconnectionidletimeout", "Pooled connection idle timeout")
            {
                IsRequired = false
            };

            public static Option<int?> ClientTimeoutOption { get; } = new Option<int?>(
                "--clienttimeout", "Client timeout in seconds")
            {
                IsRequired = false
            };
        }

        public static class LPSWatchdogCommandOptions
        {
            static LPSWatchdogCommandOptions()
            {
                // Shortcut aliases
                MaxMemoryMB.AddAlias("-mmm");
                MaxCPUPercentage.AddAlias("-mcp");
                CoolDownMemoryMB.AddAlias("-cdmm");
                CoolDownCPUPercentage.AddAlias("-cdcp");
                MaxConcurrentConnectionsCountPerHostName.AddAlias("-mcccphn");
                CoolDownConcurrentConnectionsCountPerHostName.AddAlias("-cdcccphn");
                MaxCoolingPeriod.AddAlias("-maxcp");
                ResumeCoolingAfter.AddAlias("-rca");
                CoolDownRetryTimeInSeconds.AddAlias("-cdrtis");
                SuspensionMode.AddAlias("-sm");

                // Add case-insensitive aliases
                AddCaseInsensitiveAliases(MaxMemoryMB, "--maxmemorymb");
                AddCaseInsensitiveAliases(MaxCPUPercentage, "--maxcpupercentage");
                AddCaseInsensitiveAliases(CoolDownMemoryMB, "--cooldownmemorymb");
                AddCaseInsensitiveAliases(CoolDownCPUPercentage, "--cooldowncpupercentage");
                AddCaseInsensitiveAliases(MaxConcurrentConnectionsCountPerHostName, "--maxconcurrentconnectionscountperhostname");
                AddCaseInsensitiveAliases(CoolDownConcurrentConnectionsCountPerHostName, "--cooldownconcurrentconnectionscountperhostname");
                AddCaseInsensitiveAliases(CoolDownRetryTimeInSeconds, "--cooldownretrytimeinseconds");
                AddCaseInsensitiveAliases(MaxCoolingPeriod, "--maxcoolingperiod");
                AddCaseInsensitiveAliases(ResumeCoolingAfter, "--resumecoolingafter");
                AddCaseInsensitiveAliases(SuspensionMode, "--suspensionmode");
            }

            public static Option<int?> MaxMemoryMB { get; } = new Option<int?>(
                "--maxmemorymb", "Memory threshold in MB")
            {
                IsRequired = false
            };

            public static Option<int?> MaxCPUPercentage { get; } = new Option<int?>(
                "--maxcpupercentage", "CPU threshold percentage")
            {
                IsRequired = false
            };

            public static Option<int?> CoolDownMemoryMB { get; } = new Option<int?>(
                "--cooldownmemorymb", "Memory cooldown in MB")
            {
                IsRequired = false
            };

            public static Option<int?> CoolDownCPUPercentage { get; } = new Option<int?>(
                "--cooldowncpupercentage", "CPU cooldown percentage")
            {
                IsRequired = false
            };

            public static Option<int?> MaxConcurrentConnectionsCountPerHostName { get; } = new Option<int?>(
                "--maxconcurrentconnectionscountperhostname", "Max concurrent connections per hostname")
            {
                IsRequired = false
            };

            public static Option<int?> CoolDownConcurrentConnectionsCountPerHostName { get; } = new Option<int?>(
                "--cooldownconcurrentconnectionscountperhostname", "Cooldown concurrent connections per hostname")
            {
                IsRequired = false
            };

            public static Option<int?> CoolDownRetryTimeInSeconds { get; } = new Option<int?>(
                "--cooldownretrytimeinseconds", "Cooldown retry interval in seconds")
            {
                IsRequired = false
            };

            public static Option<int?> MaxCoolingPeriod { get; } = new Option<int?>(
                "--maxcoolingperiod", "Maximum cooling period in seconds")
            {
                IsRequired = false
            };

            public static Option<int?> ResumeCoolingAfter { get; } = new Option<int?>(
                "--resumecoolingafter", "Resume cooling after seconds")
            {
                IsRequired = false
            };

            public static Option<SuspensionMode?> SuspensionMode { get; } = new Option<SuspensionMode?>(
                "--suspensionmode", "Suspension approach ('All' or 'Any')")
            {
                IsRequired = false
            };
        }

        public static void AddOptionsToCommand(Command command, Type optionsType)
        {
            var properties = optionsType.GetProperties(
                BindingFlags.Public | BindingFlags.Static);

            foreach (var property in properties)
            {
                if (property.PropertyType.IsGenericType &&
                    property.PropertyType.GetGenericTypeDefinition() ==
                    typeof(Option<>))
                {
                    var optionInstance = (Option)property.GetValue(null);
                    command.AddOption(optionInstance);
                }
            }
        }
    }
}
