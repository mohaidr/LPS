using LPS.Domain;
using System;
using System.Collections.Generic;
using System.Linq;
using System.CommandLine;
using static LPS.Domain.LPSHttpRun;
using LPS.Domain.Common.Interfaces;
using System.Reflection;
using LPS.Infrastructure.Watchdog;

namespace LPS.UI.Core.LPSCommandLine
{

    public static class LPSCommandLineOptions
    {
        static LPSCommandLineOptions()
        {

        }

        public static class LPSCommandOptions
        {
            static LPSCommandOptions() 
            {
                TestNameOption.AddAlias("-tn");
                DelayClientCreation.AddAlias("-dcc");
                NumberOfClientsOption.AddAlias("-nc");
                RampupPeriodOption.AddAlias("-rp");
                RunInParaller.AddAlias("-rip");
                TestNameOption.AddAlias("--testname");
                DelayClientCreation.AddAlias("--delayclientcreation");
                NumberOfClientsOption.AddAlias("--numberofclients");
                RampupPeriodOption.AddAlias("--rampupperiod");
                RunInParaller.AddAlias("--runinparallel");
                RunNameOption.AddAlias("-rn");
                RequestCountOption.AddAlias("-rc");
                Duration.AddAlias("-d");
                BatchSize.AddAlias("-bs");
                CoolDownTime.AddAlias("-cdt");
                HttpMethodOption.AddAlias("-hm");
                HttpversionOption.AddAlias("-hv");
                UrlOption.AddAlias("-u");
                HeaderOption.AddAlias("-h");
                PayloadOption.AddAlias("-p");
                IterationModeOption.AddAlias("-im");
                DownloadHtmlEmbeddedResources.AddAlias("-dhtmler");
                SaveResponse.AddAlias("-sr");
                RunNameOption.AddAlias("--runname");
                RequestCountOption.AddAlias("--requestcount");
                Duration.AddAlias("--Duration");
                BatchSize.AddAlias("--batchsize");
                CoolDownTime.AddAlias("--cooldowntime");
                HttpMethodOption.AddAlias("--httpmethod");
                HttpversionOption.AddAlias("--httpversion");
                UrlOption.AddAlias("--url");
                HeaderOption.AddAlias("--Header");
                PayloadOption.AddAlias("--Payload");
                IterationModeOption.AddAlias("--itermationmode");
                DownloadHtmlEmbeddedResources.AddAlias("--downloadhtmlembeddedresources");
                SaveResponse.AddAlias("--saveresponse");
            }
            public static Option<string> TestNameOption { get; } = new Option<string>("--testname", ()=> "Quick-Test-Plan", "Test name") { IsRequired = false, Arity = ArgumentArity.ExactlyOne };
            public static Option<int> NumberOfClientsOption { get; } = new Option<int>("--numberOfClients", () => 1, "Number of clients to execute the plan") { IsRequired = false, };
            public static Option<int> RampupPeriodOption { get; } = new Option<int>("--rampupPeriod", () => 0, "Time in millisencds to wait before a new client run the test plan") { IsRequired = false, };
            public static Option<bool> DelayClientCreation { get; } = new Option<bool>("--delayClientCreation", () => false, "Delay Client Creation Until is Needed") { IsRequired = false };
            public static Option<bool> RunInParaller { get; } = new Option<bool>("--runInParallel", () => true, "Execute your runs In Parallel") { IsRequired = false };
            public static Option<string> UrlOption { get; } = new Option<string>("--url", "URL") { IsRequired = true };
            public static Option<string> HttpMethodOption { get; } = new Option<string>("--method", ()=> "GET", "HTTP method") { IsRequired = false };
            public static Option<IList<string>> HeaderOption { get; } = new Option<IList<string>>("--header", "Header") { IsRequired = false, AllowMultipleArgumentsPerToken = true, };
            public static Option<string> RunNameOption { get; } = new Option<string>("--runName", ()=> "Quick-Http-Run", "The name of the 'HTTP Run'") { IsRequired = false };
            public static Option<IterationMode> IterationModeOption { get; } = new Option<IterationMode>("--iterationMode", ()=> IterationMode.R, "It defines the 'HTTP Run' iteration Mode") { IsRequired = false, };
            public static Option<int?> RequestCountOption { get; } = new Option<int?>("--requestCount", ()=> null, "The number of requests") { IsRequired = false, };
            public static Option<int?> Duration { get; } = new Option<int?>("--duration", ()=> null, "Time to run in seconds") { IsRequired = false, };
            public static Option<int?> CoolDownTime { get; } = new Option<int?>("--coolDownTime", ()=> null, "The time in seconds the client has to cooldown before sending the next batch") { IsRequired = false, };
            public static Option<int?> BatchSize { get; } = new Option<int?>("--batchsize", ()=> null, "The number of requests to be sent in a batch") { IsRequired = false, };
            public static Option<string> HttpversionOption { get; } = new Option<string>("--httpVersion", () => "2.0", "HTTP version") { IsRequired = false };
            public static Option<bool> DownloadHtmlEmbeddedResources { get; } = new Option<bool>("--downloadHtmlEmbeddedResources", () => false, "Set to true to download the HTML embedded resources, otherwise false ") { IsRequired = false };
            public static Option<bool> SaveResponse { get; } = new Option<bool>("--saveResponse", () => false, "Set to true to save the http response, otherwise false") { IsRequired = false };
            public static Option<string> PayloadOption { get; } = new Option<string>("--payload", ()=> string.Empty, "Request Payload, can be a path to a file or inline text") { IsRequired = false, };

        }

        public static class LPSCreateCommandOptions
        {
            static LPSCreateCommandOptions()
            {
                TestNameOption.AddAlias("-tn");
                DelayClientCreation.AddAlias("-dcc");
                NumberOfClientsOption.AddAlias("-nc");
                RampupPeriodOption.AddAlias("-rp");
                RunInParaller.AddAlias("-rip");
                TestNameOption.AddAlias("--testname");
                DelayClientCreation.AddAlias("--delayclientcreation");
                NumberOfClientsOption.AddAlias("--numberofclients");
                RampupPeriodOption.AddAlias("--rampupperiod");
                RunInParaller.AddAlias("--runinparallel");

            }
            public static Option<string> TestNameOption { get; } = new Option<string>("--testname", "Test name") { IsRequired = true, Arity = ArgumentArity.ExactlyOne };
            public static Option<int> NumberOfClientsOption { get; } = new Option<int>("--numberOfClients", "Number of clients to execute the plan") { IsRequired = true, };
            public static Option<int> RampupPeriodOption { get; } = new Option<int>("--rampupPeriod", "Time in millisencds to wait before creating starting a new client") { IsRequired = true, };
            public static Option<bool> DelayClientCreation { get; } = new Option<bool>("--delayClientCreation", () => false, "Delay Client Creation Until is Needed") { IsRequired = false };
            public static Option<bool> RunInParaller { get; } = new Option<bool>("--runInParallel", () => true, "Execute your runs In Parallel") { IsRequired = false };
        
        }

        public static class LPSAddCommandOptions
        { 
            static LPSAddCommandOptions()
            {
                TestNameOption.AddAlias("-tn");
                RunNameOption.AddAlias("-rn");
                RequestCountOption.AddAlias("-rc");
                Duratiion.AddAlias("-d");
                BatchSize.AddAlias("-bs");
                CoolDownTime.AddAlias("-cdt");
                HttpMethodOption.AddAlias("-hm");
                HttpversionOption.AddAlias("-hv");
                UrlOption.AddAlias("-u");
                HeaderOption.AddAlias("-h");
                PayloadOption.AddAlias("-p");
                IterationModeOption.AddAlias("-im");
                DownloadHtmlEmbeddedResources.AddAlias("-dhtmler");
                SaveResponse.AddAlias("-sr");
                TestNameOption.AddAlias("--testname");
                RunNameOption.AddAlias("--runname");
                RequestCountOption.AddAlias("--requestcount");
                Duratiion.AddAlias("--Duration");
                BatchSize.AddAlias("--batchsize");
                CoolDownTime.AddAlias("--cooldowntime");
                HttpMethodOption.AddAlias("--httpmethod");
                HttpversionOption.AddAlias("--httpversion");
                UrlOption.AddAlias("--url");
                HeaderOption.AddAlias("--Header");
                PayloadOption.AddAlias("--Payload");
                IterationModeOption.AddAlias("--itermationmode");
                DownloadHtmlEmbeddedResources.AddAlias("--downloadhtmlembeddedresources");
                SaveResponse.AddAlias("--saveresponse");
            }

            public static Option<string> TestNameOption { get; } = new Option<string>("--testname", "Test name") { IsRequired = true, Arity = ArgumentArity.ExactlyOne };
            public static Option<string> RunNameOption { get; } = new Option<string>("--runName", "The name of the 'HTTP Run'") { IsRequired = true };
            public static Option<IterationMode> IterationModeOption { get; } = new Option<IterationMode>("--iterationMode", "It defines the 'HTTP Run' iteration Mode") { IsRequired = false, };
            public static Option<int?> RequestCountOption { get; } = new Option<int?>("--requestCount", "The number of requests") { IsRequired = false, };
            public static Option<int?> Duratiion { get; } = new Option<int?>("--duration", "Time to run in seconds") { IsRequired = false, };
            public static Option<int?> CoolDownTime { get; } = new Option<int?>("--coolDownTime", "The time in seconds the client has to cooldown before sending the next batch") { IsRequired = false, };
            public static Option<int?> BatchSize { get; } = new Option<int?>("--batchsize", "The number of requests to be sent in a batch") { IsRequired = false, };
            public static Option<string> HttpMethodOption { get; } = new Option<string>("--method", "HTTP method") { IsRequired = true };
            public static Option<string> HttpversionOption { get; } = new Option<string>("--httpVersion", () => "2.0", "HTTP version") { IsRequired = false };
            public static Option<string> UrlOption { get; } = new Option<string>("--url", "URL") { IsRequired = true };
            public static Option<bool> DownloadHtmlEmbeddedResources { get; } = new Option<bool>("--downloadHtmlEmbeddedResources", () => false, "Set to true to download the HTML embedded resources or false otherwise") { IsRequired = false };
            public static Option<bool> SaveResponse { get; } = new Option<bool>("--saveResponse", () => false, "Set to true to save the http response or false otherwise") { IsRequired = false };
            public static Option<IList<string>> HeaderOption { get; } = new Option<IList<string>>("--header", "Header") { IsRequired = false, AllowMultipleArgumentsPerToken = true, };
            public static Option<string> PayloadOption { get; } = new Option<string>("--payload", "Request Payload, can be a path to a file or inline text") { IsRequired = false, };

        }

        public static class LPSRunCommandOptions
        {
            static LPSRunCommandOptions()
            {
                TestNameOption.AddAlias("-tn");
                TestNameOption.AddAlias("--testname");
            }
            public static Option<string> TestNameOption { get; } = new Option<string>("--testname", "Test name") { IsRequired = true, Arity = ArgumentArity.ExactlyOne };
        }

        
        public static class LPSLoggerCommandOptions
        {
            static LPSLoggerCommandOptions()
            {
                LogFilePathOption.AddAlias("-lfp");
                LogFilePathOption.AddAlias("--logfilepath");
                DisableFileLoggingOption.AddAlias("-dfl");
                DisableFileLoggingOption.AddAlias("--disablefilelogging");
                EnableConsoleLoggingOption.AddAlias("-ecl");
                EnableConsoleLoggingOption.AddAlias("--enableconsolelogging");
                DisableConsoleErrorLoggingOption.AddAlias("-dcel");
                DisableConsoleErrorLoggingOption.AddAlias("--disableconsoleerrorlogging");
                LoggingLevelOption.AddAlias("-ll");
                LoggingLevelOption.AddAlias("--logginglevel");
                ConsoleLoggingLevelOption.AddAlias("-cll");
                ConsoleLoggingLevelOption.AddAlias("--consolelogginglevel");
            }
            public static Option<string> LogFilePathOption { get; } = new Option<string>("--logFilePath", "A path value to the log file") { IsRequired = false, };
            public static Option<bool?> EnableConsoleLoggingOption { get; } = new Option<bool?>("--enableConsoleLogging", "Set to true to enable console logging and false otherwise") { IsRequired = false };
            public static Option<bool?> DisableConsoleErrorLoggingOption { get; } = new Option<bool?>("--disableConsoleErrorLogging", "Set to true to disable console error logging and false otherwise") { IsRequired = false };
            public static Option<bool?> DisableFileLoggingOption { get; } = new Option<bool?>("--disableFileLogging", "Set to true to disable file logging and false otherwise") { IsRequired = false };
            public static Option<LPSLoggingLevel?> LoggingLevelOption { get; } = new Option<LPSLoggingLevel?>("--loggingLevel", "The logging level") { IsRequired = false, };
            public static Option<LPSLoggingLevel?> ConsoleLoggingLevelOption { get; } = new Option<LPSLoggingLevel?>("--consoleLoggingLevel", "The logging level on console") { IsRequired = false, };
        }

        public static class LPSHttpClientCommandOptions
        {
            static LPSHttpClientCommandOptions()
            {
                MaxConnectionsPerServerption.AddAlias("-mcps");
                PoolConnectionLifeTimeOption.AddAlias("-pclt");
                PoolConnectionIdelTimeoutOption.AddAlias("-pcit");
                ClientTimeoutOption.AddAlias("-cto");
                MaxConnectionsPerServerption.AddAlias("--maxconnectionsperserver");
                PoolConnectionLifeTimeOption.AddAlias("--poolconnectionlifetime");
                PoolConnectionIdelTimeoutOption.AddAlias("--PoolconnectionIdeltimeout");
                ClientTimeoutOption.AddAlias("--clienttimeout");
            }

            public static Option<int?> MaxConnectionsPerServerption { get; } = new Option<int?>("--maxConnectionsPerServer", "max number of connections per client per server") { IsRequired = false, };
            public static Option<int?> PoolConnectionLifeTimeOption { get; } = new Option<int?>("--poolConnectionLifeTime", "Pooled connection life time defines the maximal connection lifetime in the pool, tracking its age from when the connection was established, regardless of how much time it spent idle or active.\nSee this link for more details https://learn.microsoft.com/en-us/dotnet/api/system.net.http.socketshttphandler.pooledconnectionlifetime?view=net-8.0") { IsRequired = false, };
            public static Option<int?> PoolConnectionIdelTimeoutOption { get; } = new Option<int?>("--poolConnectionIdelTimeout", "Pooled connection idle timeout defines the maximum idle time for a connection in the pool.\nSee this link for more details https://learn.microsoft.com/en-us/dotnet/api/system.net.http.socketshttphandler.pooledconnectionidletimeout?view=net-8.0") { IsRequired = false, };
            public static Option<int?> ClientTimeoutOption { get; } = new Option<int?>("--clientTimeout", "Timeout") { IsRequired = false };

        }

        public static class LPSWatchdogCommandOptions
        {
            static LPSWatchdogCommandOptions() 
            {
                MaxMemoryMB.AddAlias("-mmm");
                MaxCPUPercentage.AddAlias("-mcp");
                CoolDownMemoryMB.AddAlias("-cdmm");
                MaxConcurrentConnectionsCountPerHostName.AddAlias("-mcccphn");
                CoolDownConcurrentConnectionsCountPerHostName.AddAlias("-cdcccphn");
                CoolDownRetryTimeInSeconds.AddAlias("-cdrtis");
                SuspensionMode.AddAlias("-sm");
                MaxMemoryMB.AddAlias("--maxmemorymb");
                MaxCPUPercentage.AddAlias("--maxcpupercentage");
                CoolDownMemoryMB.AddAlias("--cooldownmemorymb");
                MaxConcurrentConnectionsCountPerHostName.AddAlias("--cooldowncpupercentage");
                CoolDownConcurrentConnectionsCountPerHostName.AddAlias("--cooldownconcurrentconnectionscountperhostname");
                CoolDownRetryTimeInSeconds.AddAlias("--cooldownretrytimeinseconds");
                SuspensionMode.AddAlias("--suspensionmode");
            }

            public static Option<int?> MaxMemoryMB { get; } = new Option<int?>("--maxMemoryMB", "Set the memory threshold in MB to puase the test when the value reached") { IsRequired = false, };
            public static Option<int?> MaxCPUPercentage { get; } = new Option<int?>("--maxCPUPercentage", "Set the CPU threshold to puase the test when the value reached") { IsRequired = false, };
            public static Option<int?> CoolDownMemoryMB { get; } = new Option<int?>("--coolDownMemoryMB", "Set the memory cooldown in MB to resume the test when the value reached") { IsRequired = false, };
            public static Option<int?> CoolDownCPUPercentage { get; } = new Option<int?>("--coolDownCPUPercentage", "Set the CPU cooldown to resume the test when the value reached") { IsRequired = false, };
            public static Option<int?> MaxConcurrentConnectionsCountPerHostName { get; } = new Option<int?>("--maxConcurrentConnectionsCountPerHostName", "Set max concurrent connections threshold to puase the test when the value reached") { IsRequired = false, };
            public static Option<int?> CoolDownConcurrentConnectionsCountPerHostName { get; } = new Option<int?>("--coolDownConcurrentConnectionsCountPerHostName", "Set max concurrent connections cooldown to resume the test when the value reached") { IsRequired = false, };
            public static Option<int?> CoolDownRetryTimeInSeconds { get; } = new Option<int?>("--coolDownRetryTimeInSeconds", "The interval at which the system examines the cooldown value to decide whether to release or persist in pausing the test.") { IsRequired = false, };
            public static Option<SuspensionMode?> SuspensionMode { get; } = new Option<SuspensionMode?>("--suspensionMode", @"determines the suspension approach. ""All"" entails pausing the test when all parameters surpass their thresholds, while ""Any"" involves pausing the test if any parameter exceeds its specified threshold.") { IsRequired = false, };

        }

        public static void AddOptionsToCommand(Command command, Type optionsType)
        {
            var commonProperties = typeof(LPSCommandLineOptions).GetProperties(BindingFlags.Public | BindingFlags.Static);
            var properties = optionsType.GetProperties(BindingFlags.Public | BindingFlags.Static);
            var allProperties = commonProperties.Concat(properties);

            foreach (var property in allProperties)
            {
                if (property.PropertyType.IsGenericType && property.PropertyType.GetGenericTypeDefinition() == typeof(Option<>))
                {
                    var optionInstance = (Option)property.GetValue(null);
                    command.Add(optionInstance);
                }
            }
        }
    }
}

