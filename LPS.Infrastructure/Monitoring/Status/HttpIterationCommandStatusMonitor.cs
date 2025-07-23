using Grpc.Core;
using LPS.Domain;
using LPS.Domain.Common.Interfaces;
using LPS.Domain.Domain.Common.Enums;
using LPS.Domain.Domain.Common.Interfaces;
using LPS.Infrastructure.GRPCClients;
using LPS.Infrastructure.GRPCClients.Factory;
using LPS.Infrastructure.Logger;
using LPS.Infrastructure.Nodes;
using Spectre.Console;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace LPS.Infrastructure.Monitoring.Status
{
    public class HttpIterationCommandStatusMonitor: ICommandStatusMonitor<HttpIteration>
    {
        private readonly ICommandRepository<IAsyncCommand<HttpIteration>, HttpIteration> _commandRepo;
        private readonly IEntityDiscoveryService _entityDiscoveryService;
        private readonly INodeRegistry _nodeRegistry;
        private readonly INodeMetadata _nodeMetadata;
        private readonly ICustomGrpcClientFactory _grpcClientFactory;
        private readonly IRuntimeOperationIdProvider _operationIdProvider;
        private readonly ILogger _logger;

        public HttpIterationCommandStatusMonitor(
            ICommandRepository<IAsyncCommand<HttpIteration>, HttpIteration> commandRepo,
            IEntityDiscoveryService entityDiscoveryService,
            INodeRegistry nodeRegistry,
            INodeMetadata nodeMetadata,
            ICustomGrpcClientFactory grpcClientFactory,
            IRuntimeOperationIdProvider operationIdProvider,
            ILogger logger)
        {
            _commandRepo = commandRepo;
            _entityDiscoveryService = entityDiscoveryService;
            _nodeRegistry = nodeRegistry;
            _nodeMetadata = nodeMetadata;
            _grpcClientFactory = grpcClientFactory;
            _operationIdProvider = operationIdProvider;
            _logger = logger;
        }

        public async ValueTask<bool> IsAnyCommandOngoing(HttpIteration entity)
        {
            var commands = _commandRepo.GetCommands(entity);
            var remoteStatuses = await GetRemoteStatusesAsync(entity);
            return commands.Any(c => c.Status == CommandExecutionStatus.Ongoing) || remoteStatuses.Contains(CommandExecutionStatus.Ongoing);
        }

        public async ValueTask<List<CommandExecutionStatus>> QueryAsync(HttpIteration entity)
        {
            var local = _commandRepo.GetCommands(entity).Select(c => c.Status);
            var remote = await GetRemoteStatusesAsync(entity);
            return local.Concat(remote).ToList();
        }

        public async ValueTask<Dictionary<HttpIteration, IList<CommandExecutionStatus>>> QueryAsync(Func<HttpIteration, bool> predicate)
        {
            var result = new Dictionary<HttpIteration, IList<CommandExecutionStatus>>();
            foreach (var entity in _commandRepo.GetEntities(predicate))
            {
                var local = _commandRepo.GetCommands(entity).Select(c => c.Status);
                var remote = await GetRemoteStatusesAsync(entity);
                result[entity] = local.Concat(remote).ToList();
            }
            return result;
        }

        private async ValueTask<List<CommandExecutionStatus>> GetRemoteStatusesAsync(HttpIteration entity)
        {
            if (_nodeMetadata.NodeType != NodeType.Master)
                return [];

            var record = _entityDiscoveryService.Discover(r => r.IterationId == entity.Id).SingleOrDefault();
            if (record is null)
                return [];

            var statuses = new List<CommandExecutionStatus>();

            foreach (var node in _nodeRegistry.Query(n => n.Metadata.NodeType == NodeType.Worker && n.IsActive()))
            {
                try
                {
                    var client = _grpcClientFactory.GetClient<GrpcMonitorClient>(node.Metadata.NodeIP);
                    var remote = await client.QueryIterationStatusesAsync(record.FullyQualifiedName);
                    statuses.AddRange(remote);
                }
                catch (RpcException rpcEx)
                {
                    _logger.Log(_operationIdProvider.OperationId, $"{rpcEx.Status}\n{rpcEx.Message}\n{rpcEx.InnerException} {rpcEx.StackTrace}", LPSLoggingLevel.Error);
                    AnsiConsole.MarkupLine($"[Red][[Error]] {DateTime.Now} {rpcEx.Status}\n{rpcEx.Message}[/]");
                }
            }

            return statuses;
        }
    }

}
