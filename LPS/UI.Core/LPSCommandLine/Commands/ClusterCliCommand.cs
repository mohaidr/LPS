using LPS.Domain.Common.Interfaces;
using LPS.Infrastructure.Logger;
using LPS.UI.Common;
using LPS.UI.Common.Extensions; // for PrintValidationErrors()
using LPS.UI.Common.Options;
using Microsoft.Extensions.Options;
using System.CommandLine;
using System.Threading;
using LPS.UI.Core.LPSCommandLine.Bindings;
using LPS.UI.Core.LPSValidators; // ClusteredConfigurationValidator

namespace LPS.UI.Core.LPSCommandLine.Commands
{
    internal class ClusterCliCommand : ICliCommand
    {
        private readonly Command _root;
        private Command _clusterCmd;
        public Command Command => _clusterCmd;

        private readonly IWritableOptions<ClusterConfigurationOptions> _clusterOptions;
        private readonly ILogger _logger;
        private readonly IRuntimeOperationIdProvider _op;

#pragma warning disable CS8618
        public ClusterCliCommand(
            Command rootLpsCliCommand,
            ILogger logger,
            IRuntimeOperationIdProvider op,
            IWritableOptions<ClusterConfigurationOptions> clusterOptions)
        {
            _root = rootLpsCliCommand;
            _logger = logger;
            _op = op;
            _clusterOptions = clusterOptions;
            Setup();
        }
#pragma warning restore CS8618

        private void Setup()
        {
            _clusterCmd = new Command("cluster", "Configure cluster (master/worker) settings");
            CommandLineOptions.AddOptionsToCommand(_clusterCmd, typeof(CommandLineOptions.LPSClusterCommandOptions));
            _root.AddCommand(_clusterCmd);
        }

        public void SetHandler(CancellationToken cancellationToken)
        {
            _clusterCmd.SetHandler((ClusterConfigurationOptions incoming) =>
            {
                var current = _clusterOptions.Value;

                // CLI value ?? existing
                var candidate = new ClusterConfigurationOptions
                {
                    MasterNodeIP = incoming.MasterNodeIP ?? current.MasterNodeIP,
                    GRPCPort = incoming.GRPCPort ?? current.GRPCPort,
                    ExpectedNumberOfWorkers = incoming.ExpectedNumberOfWorkers ?? current.ExpectedNumberOfWorkers,
                    MasterNodeIsWorker = incoming.MasterNodeIsWorker ?? current.MasterNodeIsWorker
                };

                // ✅ Use FluentValidation validator
                var validator = new ClusteredConfigurationValidator();
                var validationResults = validator.Validate(candidate);

                if (!validationResults.IsValid)
                {
                    _logger.Log(
                        _op.OperationId,
                        "Invalid cluster configuration. Updating LPSAppSettings:ClusterConfiguration with the provided arguments would result in an invalid configuration. Run 'lps cluster -h' to see available options.",
                        LPSLoggingLevel.Warning);

                    validationResults.PrintValidationErrors();
                    return;
                }

                _clusterOptions.Update(opt =>
                {
                    opt.MasterNodeIP = candidate.MasterNodeIP;
                    opt.GRPCPort = candidate.GRPCPort;
                    opt.ExpectedNumberOfWorkers = candidate.ExpectedNumberOfWorkers;
                    opt.MasterNodeIsWorker = candidate.MasterNodeIsWorker;
                });

                _logger.Log(
                    _op.OperationId,
                    $"Cluster configuration updated. MasterNodeIP={candidate.MasterNodeIP}, " +
                    $"GRPCPort={candidate.GRPCPort}, ExpectedWorkers={candidate.ExpectedNumberOfWorkers}, " +
                    $"MasterNodeIsWorker={candidate.MasterNodeIsWorker}",
                    LPSLoggingLevel.Information);

            }, new ClusterBinder());
        }
    }
}
