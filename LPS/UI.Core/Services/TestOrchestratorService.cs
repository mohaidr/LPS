using Grpc.Net.Client;
using LPS.Common.Interfaces;
using LPS.Domain.Common.Interfaces;
using LPS.Infrastructure.GRPCClients;
using LPS.Infrastructure.GRPCClients.Factory;
using LPS.Infrastructure.Nodes;
using LPS.Infrastructure.Services;
using LPS.Protos.Shared;
using LPS.UI.Common;
using LPS.UI.Core.LPSCommandLine;
using System;
using System.Collections.Generic;
using System.CommandLine;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Node = LPS.Infrastructure.Nodes.Node;
using NodeType = LPS.Infrastructure.Nodes.NodeType;

namespace LPS.UI.Core.Services
{
    public class TestOrchestratorService: ITestOrchestratorService, ITestTriggerObserver
    {
        private readonly ITestTriggerNotifier _testTriggerNotifier;
        private readonly IClusterConfiguration _clusterConfiguration;
        private readonly ILogger _logger;
        private readonly INodeRegistry _nodeRegistry;
        private readonly IRuntimeOperationIdProvider _runtimeOperationIdProvider;
        private readonly ITestExecutionService _testExecutionService;
        private readonly ICustomGrpcClientFactory _customGrpcClientFactory;
        readonly NodeHealthMonitorBackgroundService _nodeHealthMonitorBackgroundService;
        INodeMetadata _nodeMetadata;
        TestRunParameters _parameters;
        public TestOrchestratorService(
            NodeHealthMonitorBackgroundService nodeHealthMonitorBackgroundService,
            ILogger logger,
            IRuntimeOperationIdProvider runtimeOperationIdProvider,
            INodeRegistry nodeRegistry,
            IClusterConfiguration clusterConfiguration,
            ITestExecutionService testExecutionService,
            ITestTriggerNotifier testTriggerNotifier,
            ICustomGrpcClientFactory customGrpcClientFactory,
            INodeMetadata nodeMetadata)
        {
            _clusterConfiguration = clusterConfiguration;
            _testTriggerNotifier = testTriggerNotifier;
            _testExecutionService = testExecutionService;
            _runtimeOperationIdProvider = runtimeOperationIdProvider;
            _nodeRegistry = nodeRegistry;
            _customGrpcClientFactory = customGrpcClientFactory;
            _logger = logger;
            _nodeHealthMonitorBackgroundService = nodeHealthMonitorBackgroundService;
            _nodeMetadata = nodeMetadata;
        } 
        public async Task RunAsync(TestRunParameters parameters)
        {
            RegisterLocalNode();
            _ = Task.Run(async () => { await _nodeHealthMonitorBackgroundService.StartAsync(parameters.CancellationToken); });

            _parameters = parameters;
            var localNode = _nodeRegistry.GetLocalNode();
            if (localNode.Metadata.NodeType == Infrastructure.Nodes.NodeType.Master)
            {
                if (_clusterConfiguration.MasterNodeIsWorker)
                {
                    await localNode.SetNodeStatus(Infrastructure.Nodes.NodeStatus.Ready);
                    await _testExecutionService.ExecuteAsync(parameters);
                }
                else
                {
                    await localNode.SetNodeStatus(Infrastructure.Nodes.NodeStatus.Ready);
                }
                // notify slave nodes to run
                foreach (var node in _nodeRegistry.Query(node => node.Metadata.NodeType == Infrastructure.Nodes.NodeType.Worker))
                {
                    // Create the gRPC Client
                    var client = _customGrpcClientFactory.GetClient<GrpcNodeClient>(node.Metadata.NodeIP);
                    var response = await client.TriggerTestAsync(new TriggerTestRequest());
                }
            }
            else
            {

                // Create the gRPC Client
                var client = _customGrpcClientFactory.GetClient<GrpcNodeClient>(_clusterConfiguration.MasterNodeIP);
                var masterNodeStatus = await client.GetNodeStatusAsync(new GetNodeStatusRequest() { });

                if (masterNodeStatus.Status == Protos.Shared.NodeStatus.Running || masterNodeStatus.Status == Protos.Shared.NodeStatus.Ready)
                {
                    await _testExecutionService.ExecuteAsync(parameters);
                }
                else
                {
                    await localNode.SetNodeStatus(Infrastructure.Nodes.NodeStatus.Ready);
                    _testTriggerNotifier.RegisterObserver(this);
                }
            }
        }
        private void RegisterLocalNode()
        {
            Node node = _nodeMetadata.NodeType == NodeType.Master ? new MasterNode(_nodeMetadata, _clusterConfiguration, _nodeRegistry, _customGrpcClientFactory) : new WorkerNode(_nodeMetadata, _clusterConfiguration, _nodeRegistry, _customGrpcClientFactory);
            // keep this line for the worker nodes to register the nodes locally
            _nodeRegistry.RegisterNode(node); // register locally
        }

        public async Task OnTestTriggered()
        {
            if(_parameters == null)
                throw new ArgumentNullException("parameters");
            await _testExecutionService.ExecuteAsync(this._parameters);
        }
    }
}
