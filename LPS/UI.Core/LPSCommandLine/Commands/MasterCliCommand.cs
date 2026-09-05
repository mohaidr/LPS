using LPS.Domain.Common.Interfaces;
using LPS.Infrastructure.Nodes;
using LPS.UI.Common;
using LPS.UI.Core.Host;
using System.CommandLine;

namespace LPS.UI.Core.LPSCommandLine.Commands
{
    internal class MasterCliCommand : ICliCommand
    {
        private readonly INodeRegistry _nodeRegistry;
        private readonly IClusterConfiguration _clusterConfiguration;
        private readonly ITestExecutionService _testExecutionService;
        private readonly IDashboardService _dashboardService;
        private readonly ILogger _logger;
        private readonly IRuntimeOperationIdProvider _runtimeOperationIdProvider;
        private readonly Argument<string> _planArgument;
        private Command _masterCommand;

        public Command Command => _masterCommand;

#pragma warning disable CS8618
        internal MasterCliCommand(
#pragma warning restore CS8618
            Command rootCliCommand,
            INodeRegistry nodeRegistry,
            IClusterConfiguration clusterConfiguration,
            ITestExecutionService testExecutionService,
            IDashboardService dashboardService,
            ILogger logger,
            IRuntimeOperationIdProvider runtimeOperationIdProvider)
        {
            _nodeRegistry = nodeRegistry;
            _clusterConfiguration = clusterConfiguration;
            _testExecutionService = testExecutionService;
            _dashboardService = dashboardService;
            _logger = logger;
            _runtimeOperationIdProvider = runtimeOperationIdProvider;

            _masterCommand = new Command("master", "Start LPS as a dedicated master node");
            _planArgument = new Argument<string>("plan", "Path to the test plan used by the workers");
            _masterCommand.AddArgument(_planArgument);
            rootCliCommand.AddCommand(_masterCommand);
        }

        public void SetHandler(CancellationToken cancellationToken)
        {
            _masterCommand.SetHandler(async (string plan) =>
            {
                var localNode = _nodeRegistry.GetLocalNode();
                if (localNode.Metadata.NodeType != NodeType.Master)
                {
                    _logger.Log(
                        _runtimeOperationIdProvider.OperationId,
                        $"This node is not the configured master. Its IP is {localNode.Metadata.NodeIP} and the configured master IP is {_clusterConfiguration.MasterNodeIP}.",
                        LPSLoggingLevel.Error);
                    return;
                }

                if (!await _testExecutionService.PrepareAsync(new TestRunParameters(plan, [], [], [], cancellationToken)))
                {
                    await localNode.SetNodeStatus(NodeStatus.Failed);
                    return;
                }

                await localNode.SetNodeStatus(NodeStatus.Ready);
                _dashboardService.Start();
                await _logger.LogAsync(
                    _runtimeOperationIdProvider.OperationId,
                    $"Master node is ready at {localNode.Metadata.NodeIP}. Press Ctrl+C or Escape to stop.",
                    LPSLoggingLevel.Information,
                    cancellationToken);

                try
                {
                    var workersToWaitFor = Math.Max(1, _clusterConfiguration.ExpectedNumberOfWorkers);
                    while (_nodeRegistry.Query(node => node.Metadata.NodeType == NodeType.Worker).Count() < workersToWaitFor)
                    {
                        await Task.Delay(500, cancellationToken);
                    }

                    while (_nodeRegistry.Query(node => node.Metadata.NodeType == NodeType.Worker && node.IsActive()).Any())
                    {
                        await Task.Delay(500, cancellationToken);
                    }
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                }
            }, _planArgument);
        }
    }
}
