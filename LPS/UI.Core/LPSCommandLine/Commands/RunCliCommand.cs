using LPS.Domain;
using LPS.Domain.Common.Interfaces;
using LPS.DTOs;
using LPS.Infrastructure.Common;
using LPS.Infrastructure.LPSClients.GlobalVariableManager;
using LPS.Infrastructure.LPSClients.SessionManager;
using LPS.Infrastructure.Monitoring;
using LPS.UI.Common;
using LPS.UI.Core.Services;
using LPS.UI.Core.LPSValidators;
using Microsoft.Extensions.Options;
using System;
using System.CommandLine;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using AutoMapper;
using LPS.Domain.Common;
using LPS.Domain.Domain.Common.Interfaces;
using LPS.Domain.LPSFlow.LPSHandlers;
using LPS.UI.Common.Options;
using static LPS.UI.Core.LPSCommandLine.CommandLineOptions;
using LPS.Domain.Domain.Common.Exceptions;
using LPS.Infrastructure.Nodes;
using Nodes;
using Grpc.Net.Client;
using Microsoft.Extensions.Hosting;
using LPS.Protos.Shared;
using NodeType = LPS.Infrastructure.Nodes.NodeType;
using System.Xml.Linq;
using Apis.Common;
using LPS.Common.Interfaces;

namespace LPS.UI.Core.LPSCommandLine.Commands
{
    internal class RunCliCommand : ICliCommand
    {
        TestRunParameters _args;

        private readonly Command _rootLpsCliCommand;
        private readonly ILogger _logger;
        private readonly IRuntimeOperationIdProvider _runtimeOperationIdProvider;
        private readonly ITestOrchestratorService _testOrchestratorService;

        private Command _runCommand;

        public Command Command => _runCommand;
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
        internal RunCliCommand(
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
            Command rootCLICommandLine,
            ILogger logger,
            IRuntimeOperationIdProvider runtimeOperationIdProvider,
            ITestOrchestratorService testOrchestratorService) // Injected AutoMapper
        {
            _rootLpsCliCommand = rootCLICommandLine;
            _runtimeOperationIdProvider = runtimeOperationIdProvider;
            _testOrchestratorService = testOrchestratorService;
            _logger = logger;
            Setup();
        }

        private void Setup()
        {
            _runCommand = new Command("run", "Run existing test");
            AddOptionsToCommand(_runCommand, typeof(LPSRunCommandOptions));
            _runCommand.AddArgument(LPSRunCommandOptions.ConfigFileArgument);
            _rootLpsCliCommand.AddCommand(_runCommand);
        }


        public void SetHandler(CancellationToken cancellationToken)
        {
            _runCommand.SetHandler(async (string configFile, IList<string> roundNames, IList<string> tags, IList<string> environments) =>
            {
                try
                {
                    var parameters = new TestRunParameters(configFile, roundNames, tags, environments, cancellationToken);
                    await _testOrchestratorService.RunAsync(parameters);

                }
                catch (Exception ex)
                {
                    _logger.Log(_runtimeOperationIdProvider.OperationId, $"{ex.Message}\r\n{ex.InnerException?.Message}\r\n{ex.StackTrace}", LPSLoggingLevel.Error);
                }
            },
            LPSRunCommandOptions.ConfigFileArgument,
            LPSRunCommandOptions.RoundNameOption,
            LPSRunCommandOptions.TagOption,
            LPSRunCommandOptions.EnvironmentOption);
        }

    }
}
