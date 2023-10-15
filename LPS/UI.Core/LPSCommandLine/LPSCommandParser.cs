using LPS.Domain;
using System;
using System.Collections.Generic;
using System.Linq;
using System.CommandLine;
using System.IO;
using Newtonsoft.Json;
using System.CommandLine.Binding;
using LPS.Domain.Common;
using LPS.UI.Common;
using System.Threading;

namespace LPS.UI.Core.LPSCommandLine
{
    public class LPSCommandParser : ILPSCommandParser<LPSTestPlan.SetupCommand, LPSTestPlan>
    {
        public string[] CommandLineArgs { get; set; }

        ILPSLogger _logger;
        LPSTestPlan.SetupCommand _command;
        ILPSClientManager<LPSHttpRequest, ILPSClientService<LPSHttpRequest>> _httpClientManager;
        ILPSClientConfiguration<LPSHttpRequest> _config;
        IRuntimeOperationIdProvider _runtimeOperationIdProvider;
        ILPSResourceTracker _resourceUsageTracker;
        public LPSCommandParser(ILPSLogger logger,
            ILPSClientManager<LPSHttpRequest, ILPSClientService<LPSHttpRequest>> httpClientManager,
            ILPSClientConfiguration<LPSHttpRequest> config,
            ILPSResourceTracker resourceUsageTracker,
            IRuntimeOperationIdProvider runtimeOperationIdProvider,
            LPSTestPlan.SetupCommand command)
        {
            _logger = logger;
            _command = command;
            _config = config;
            _httpClientManager = httpClientManager;
            _resourceUsageTracker = resourceUsageTracker;
            _runtimeOperationIdProvider = runtimeOperationIdProvider;
        }
        public LPSTestPlan.SetupCommand Command { get { return _command; } set { } }

        public void Parse(CancellationToken cancellationToken)
        {
            var lpsCommand = new Command("lps", "Load, Performance and Stress Testing Command Tool.");

            Command createCommand = new Command("create", "Create a new test") {
                CommandLineOptions.TestNameOption,
                CommandLineOptions.NumberOfClientsOption,
                CommandLineOptions.MaxConnectionsPerServerption,
                CommandLineOptions.PoolConnectionIdelTimeoutOption,
                CommandLineOptions.PoolConnectionLifeTimeOption,
                CommandLineOptions.ClientTimeoutOption,
                CommandLineOptions.RampupPeriodOption,
                CommandLineOptions.DelayClientCreation,
                CommandLineOptions.RunInParaller

            };
            Command addCommand = new Command("add", "Add an http request")
            {
                CommandLineOptions.TestNameOption,
                CommandLineOptions.CaseNameOption,
                CommandLineOptions.HttpMethodOption,
                CommandLineOptions.HttpversionOption,
                CommandLineOptions.RequestCountOption,
                CommandLineOptions.IterationModeOption,
                CommandLineOptions.Duratiion,
                CommandLineOptions.CoolDownTime,
                CommandLineOptions.BatchSize,
                CommandLineOptions.UrlOption,
                CommandLineOptions.HeaderOption,
                CommandLineOptions.PayloadOption
            };
            Command runCommand = new Command("run", "Run existing test") {
               CommandLineOptions.TestNameOption,
            };

            lpsCommand.AddCommand(createCommand);
            lpsCommand.AddCommand(addCommand);
            lpsCommand.AddCommand(runCommand);

            createCommand.SetHandler((lpaTestPlan) =>
                {
                    _command.Name = lpaTestPlan.Name;
                    _command.NumberOfClients = lpaTestPlan.NumberOfClients;
                    _command.DelayClientCreationUntilIsNeeded = lpaTestPlan.DelayClientCreationUntilIsNeeded;
                    _command.RampUpPeriod = lpaTestPlan.RampUpPeriod;
                    _command.MaxConnectionsPerServer = lpaTestPlan.MaxConnectionsPerServer;
                    _command.ClientTimeout = lpaTestPlan.ClientTimeout;
                    _command.PooledConnectionIdleTimeout = lpaTestPlan.PooledConnectionIdleTimeout;
                    _command.PooledConnectionLifetime = lpaTestPlan.PooledConnectionLifetime;
                    _command.RunInParallel = lpaTestPlan.RunInParallel;
                    string json = new LpsSerializer().Serialize(_command);
                    File.WriteAllText($"{lpaTestPlan.Name}.json", json);
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine("Test Has Been Created Successfully");
                    Console.ResetColor();
                },
                new LPSTestPlanCommandBinder(
                     CommandLineOptions.TestNameOption,
                     CommandLineOptions.NumberOfClientsOption,
                     CommandLineOptions.RampupPeriodOption,
                     CommandLineOptions.MaxConnectionsPerServerption,
                     CommandLineOptions.PoolConnectionLifeTimeOption,
                     CommandLineOptions.PoolConnectionIdelTimeoutOption,
                     CommandLineOptions.ClientTimeoutOption,
                     CommandLineOptions.DelayClientCreation,
                     CommandLineOptions.RunInParaller)); 

            addCommand.SetHandler(
                (testName, lpsTestCase) =>
                {
                    var serializer = new LpsSerializer();
                    _command = serializer.DeSerialize(File.ReadAllText($"{testName}.json"));
                    _command.LPSTestCases.Add(lpsTestCase);
                    _command.IsValid = true;
                    string json = serializer.Serialize(_command);
                    File.WriteAllText($"{testName}.json", json);
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine("Request Has Been Added Successfully");
                    Console.ResetColor();
                },
                CommandLineOptions.TestNameOption,
                new LPSTestCaseCommandBinder(
                CommandLineOptions.CaseNameOption,
                CommandLineOptions.RequestCountOption,
                CommandLineOptions.IterationModeOption,
                CommandLineOptions.Duratiion,
                CommandLineOptions.CoolDownTime,
                CommandLineOptions.BatchSize,
                CommandLineOptions.HttpMethodOption,
                CommandLineOptions.HttpversionOption,
                CommandLineOptions.UrlOption,
                CommandLineOptions.HeaderOption,
                CommandLineOptions.PayloadOption,
                CommandLineOptions.DownloadHtmlEmbeddedResources));

            runCommand.SetHandler(async (testName) =>
            {
                _command = new LpsSerializer().DeSerialize(File.ReadAllText($"{testName}.json"));
                await new LPSManager(_logger, _httpClientManager, _config, _resourceUsageTracker, _runtimeOperationIdProvider).Run(_command, cancellationToken);
            }, CommandLineOptions.TestNameOption);

            lpsCommand.Invoke(CommandLineArgs);
        }
    }
}
