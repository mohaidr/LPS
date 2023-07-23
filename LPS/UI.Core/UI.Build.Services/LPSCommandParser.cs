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

namespace LPS.UI.Core.UI.Build.Services
{
    public class LPSCommandParser : ILPSCommandParser<LPSTestPlan.SetupCommand, LPSTestPlan>
    {
        public string[] CommandLineArgs { get; set; }

        ILPSLogger _logger;
        LPSTestPlan.SetupCommand _command;
        ILPSClientManager<LPSHttpRequest, ILPSClientService<LPSHttpRequest>> _httpClientManager;
        ILPSClientConfiguration<LPSHttpRequest> _config;
        IRuntimeOperationIdProvider _runtimeOperationIdProvider;
        public LPSCommandParser(ILPSLogger logger,
            ILPSClientManager<LPSHttpRequest, ILPSClientService<LPSHttpRequest>> httpClientManager,
            ILPSClientConfiguration<LPSHttpRequest> config,
            IRuntimeOperationIdProvider runtimeOperationIdProvider,
            LPSTestPlan.SetupCommand command)
        {
            _logger = logger;
            _command = command;
            _config = config;
            _httpClientManager = httpClientManager;
            _runtimeOperationIdProvider = runtimeOperationIdProvider;
        }
        public LPSTestPlan.SetupCommand Command { get { return _command; } set { } }

        public void Parse(CancellationToken cancellationToken)
        {
            var lpsCommand = new Command("lps", "Load, Performance and Stress Testing Command Tool.");

            Command createCommand = new Command("create", "Create a new test") {
                CommandLineOptions.TestNameOption,
                CommandLineOptions.NumberOfClients,
                CommandLineOptions.MaxConnectionsPerServer,
                CommandLineOptions.PoolConnectionIdelTimeout,
                CommandLineOptions.PoolConnectionLifeTime,
                CommandLineOptions.ClientTimeoutOption,
                CommandLineOptions.RampupPeriod,
                CommandLineOptions.DelayClientCreation

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

            createCommand.SetHandler((testName, 
                numberofClients,
                maxConnectionsPerServer,
                poolConnectionIdelTimeout,
                poolConnectionLifeTime, 
                clientTimeoutOption,
                rampUpPeriod,
                delayClientCreation) =>
                {
                    _command.Name = testName;
                    _command.NumberOfClients= numberofClients;
                    _command.DelayClientCreationUntilIsNeeded= delayClientCreation;
                    _command.RampUpPeriod= rampUpPeriod;
                    _command.MaxConnectionsPerServer= maxConnectionsPerServer;
                    _command.ClientTimeout= clientTimeoutOption;
                    _command.PooledConnectionIdleTimeout= poolConnectionIdelTimeout;
                    _command.PooledConnectionLifetime= poolConnectionLifeTime;
                    string json = new LpsSerializer().Serialize(_command);
                    File.WriteAllText($"{testName}.json", json);
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine("Test Has Been Created Successfully");
                    Console.ResetColor();
                },
                CommandLineOptions.TestNameOption, 
                CommandLineOptions.NumberOfClients,
                CommandLineOptions.MaxConnectionsPerServer,
                CommandLineOptions.PoolConnectionIdelTimeout,
                CommandLineOptions.PoolConnectionLifeTime,
                CommandLineOptions.ClientTimeoutOption,
                CommandLineOptions.RampupPeriod,
                CommandLineOptions.DelayClientCreation);

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
                new LPSTestCaseBinder(
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
                CommandLineOptions.PayloadOption));

            runCommand.SetHandler(async (testName) =>
            {
                _command = new LpsSerializer().DeSerialize(File.ReadAllText($"{testName}.json"));
                await new LPSManager(_logger, _httpClientManager, _config, _runtimeOperationIdProvider).Run(_command, cancellationToken);
            }, CommandLineOptions.TestNameOption);

            lpsCommand.Invoke(CommandLineArgs);
        }
    }
}
