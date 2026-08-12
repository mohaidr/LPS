#nullable enable
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LPS.Domain;
using LPS.Infrastructure.GRPCClients;
using LPS.Infrastructure.GRPCClients.Factory;
using LPS.Infrastructure.Nodes;
using LPS.Protos.Shared;
using ProtoHostKey = LPS.Protos.Shared.HostKey;
using ProtoDurationMetricType = LPS.Protos.Shared.DurationMetricType;
using DurationMetricType = LPS.Infrastructure.Monitoring.Metrics.DurationMetricType;
using NodeType = LPS.Infrastructure.Nodes.NodeType;

namespace LPS.Infrastructure.Monitoring.Hosts
{
    public sealed class HostMetricsService : IHostMetricsService
    {
        private readonly INodeMetadata _nodeMetadata;
        private readonly IHostMetricsAggregatorFactory _factory;
        private readonly IEntityDiscoveryService _entityDiscoveryService;
        private readonly GrpcHostMetricsClient? _grpcClient;

        public HostMetricsService(
            INodeMetadata nodeMetadata,
            IHostMetricsAggregatorFactory factory,
            IEntityDiscoveryService entityDiscoveryService,
            IClusterConfiguration clusterConfiguration,
            ICustomGrpcClientFactory customGrpcClientFactory)
        {
            _nodeMetadata = nodeMetadata;
            _factory = factory;
            _entityDiscoveryService = entityDiscoveryService;

            if (_nodeMetadata.NodeType != NodeType.Master)
                _grpcClient = customGrpcClientFactory.GetClient<GrpcHostMetricsClient>(clusterConfiguration.MasterNodeIP);
        }

        private bool IsWorker => _nodeMetadata.NodeType != NodeType.Master;

        public async ValueTask IncreaseConnectionsCountAsync(HostKey hostKey, Guid requestId, CancellationToken token)
        {
            if (IsWorker)
            {
                await _grpcClient!.IncreaseConnectionsAsync(new HostIncreaseConnectionsRequest
                {
                    HostKey = ToProto(hostKey),
                    RequestId = requestId.ToString()
                }, cancellationToken: token);
                return;
            }

            // On the master, associate the host with its iteration (translated from the worker's requestId) for status.
            var masterRequestId = ResolveMasterRequestId(requestId);
            var aggregator = masterRequestId != Guid.Empty
                ? _factory.GetOrCreate(hostKey, masterRequestId)
                : _factory.GetOrCreate(hostKey);
            await aggregator.IncreaseConnectionsCountAsync(token);
        }

        public async ValueTask DecreaseConnectionsCountAsync(HostKey hostKey, CancellationToken token)
        {
            if (IsWorker)
            {
                await _grpcClient!.DecreaseConnectionsAsync(new HostDecreaseConnectionsRequest { HostKey = ToProto(hostKey) }, cancellationToken: token);
                return;
            }
            await _factory.GetOrCreate(hostKey).DecreaseConnectionsCountAsync(token);
        }

        public async ValueTask IncreaseSkippedRequestsCountAsync(HostKey hostKey, CancellationToken token)
        {
            if (IsWorker)
            {
                await _grpcClient!.IncreaseSkippedRequestsAsync(new HostSkippedRequestsRequest { HostKey = ToProto(hostKey) }, cancellationToken: token);
                return;
            }
            await _factory.GetOrCreate(hostKey).IncreaseSkippedRequestsCountAsync(token);
        }

        public async ValueTask UpdateResponseAsync(HostKey hostKey, HttpResponse.SetupCommand response, CancellationToken token)
        {
            ArgumentNullException.ThrowIfNull(response);
            if (IsWorker)
            {
                await _grpcClient!.UpdateResponseAsync(new HostUpdateResponseRequest
                {
                    HostKey = ToProto(hostKey),
                    StatusCode = (int)response.StatusCode,
                    StatusReason = response.StatusMessage ?? response.StatusCode.ToString(),
                    IsSuccess = response.IsSuccessStatusCode
                }, cancellationToken: token);
                return;
            }
            await _factory.GetOrCreate(hostKey).UpdateResponseAsync(response, token);
        }

        public async ValueTask UpdateDurationAsync(HostKey hostKey, DurationMetricType metricType, double valueMs, CancellationToken token)
        {
            if (IsWorker)
            {
                await _grpcClient!.UpdateDurationAsync(new HostUpdateDurationRequest
                {
                    HostKey = ToProto(hostKey),
                    MetricType = ToProto(metricType),
                    ValueMs = valueMs
                }, cancellationToken: token);
                return;
            }
            await _factory.GetOrCreate(hostKey).UpdateDurationAsync(metricType, valueMs, token);
        }

        public async ValueTask UpdateDataSentAsync(HostKey hostKey, double totalBytes, CancellationToken token)
        {
            if (IsWorker)
            {
                await _grpcClient!.UpdateDataTransmissionAsync(new HostUpdateDataTransmissionRequest
                {
                    HostKey = ToProto(hostKey),
                    DataSize = totalBytes,
                    IsSent = true
                }, cancellationToken: token);
                return;
            }
            await _factory.GetOrCreate(hostKey).UpdateDataSentAsync(totalBytes, token);
        }

        public async ValueTask UpdateDataReceivedAsync(HostKey hostKey, double totalBytes, CancellationToken token)
        {
            if (IsWorker)
            {
                await _grpcClient!.UpdateDataTransmissionAsync(new HostUpdateDataTransmissionRequest
                {
                    HostKey = ToProto(hostKey),
                    DataSize = totalBytes,
                    IsSent = false
                }, cancellationToken: token);
                return;
            }
            await _factory.GetOrCreate(hostKey).UpdateDataReceivedAsync(totalBytes, token);
        }

        private static ProtoHostKey ToProto(HostKey hostKey) => new()
        {
            Scheme = hostKey.Scheme,
            Host = hostKey.Host,
            Port = hostKey.Port
        };

        private static ProtoDurationMetricType ToProto(DurationMetricType metricType) => metricType switch
        {
            DurationMetricType.TotalTime => ProtoDurationMetricType.TotalTime,
            DurationMetricType.ReceivingTime => ProtoDurationMetricType.ReceivingTime,
            DurationMetricType.SendingTime => ProtoDurationMetricType.SendingTime,
            DurationMetricType.TLSHandshakeTime => ProtoDurationMetricType.TlsHandshakeTime,
            DurationMetricType.TCPHandshakeTime => ProtoDurationMetricType.TcpHandshakeTime,
            DurationMetricType.TimeToFirstByte => ProtoDurationMetricType.TimeToFirstByte,
            DurationMetricType.WaitingTime => ProtoDurationMetricType.WaitingTime,
            DurationMetricType.ServerTime => ProtoDurationMetricType.ServerTime,
            DurationMetricType.ServerTimeDB => ProtoDurationMetricType.ServerTimeDb,
            DurationMetricType.ServerTimeCache => ProtoDurationMetricType.ServerTimeCache,
            DurationMetricType.ServerTimeApp => ProtoDurationMetricType.ServerTimeApp,
            _ => ProtoDurationMetricType.TotalTime
        };

        // Mirrors MetricsService.DiscoverRequestIdOnMaster: translate a worker requestId to the master's matching one.
        private Guid ResolveMasterRequestId(Guid requestId)
        {
            var record = _entityDiscoveryService.Discover(r => r.RequestId == requestId)?.SingleOrDefault();
            if (record is null)
                return Guid.Empty;

            if (_nodeMetadata.NodeType == NodeType.Worker)
                return requestId;

            if (record.Node.Metadata.NodeType != NodeType.Master)
            {
                var fqn = record.FullyQualifiedName;
                var masterRecord = _entityDiscoveryService
                    .Discover(r => r.Node.Metadata.NodeType == NodeType.Master && r.FullyQualifiedName == fqn)
                    ?.SingleOrDefault();
                return masterRecord?.RequestId ?? Guid.Empty;
            }

            return requestId;
        }
    }
}
