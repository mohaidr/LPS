using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Grpc.Net.Client;
using LPS.Domain.Common.Interfaces;
using LPS.Domain;
using LPS.Infrastructure.Logger;
using LPS.Infrastructure.Common.Interfaces;
using LPS.Infrastructure.Nodes;
using static LPS.Protos.Shared.MetricsProtoService;
using LPS.Protos.Shared;
using LPS.Infrastructure.GRPCClients.Factory;
using LPS.Infrastructure.GRPCClients;
using LPS.Infrastructure.Monitoring.Metrics;
using ProtoDurationMetricType = LPS.Protos.Shared.DurationMetricType;
using DurationMetricType = LPS.Infrastructure.Monitoring.Metrics.DurationMetricType;


namespace LPS.Infrastructure.Monitoring.MetricsServices
{
    public class MetricsService : IMetricsService
    {
        private readonly ILogger _logger;
        private readonly IRuntimeOperationIdProvider _runtimeOperationIdProvider;
        private readonly ConcurrentDictionary<string, IReadOnlyList<IMetricAggregator>> _aggregators = new();
        private readonly IMetricAggregatorFactory _metricAggregatorFactory;
        private readonly INodeMetadata _nodeMetaData;
        private readonly IEntityDiscoveryService _entityDiscoveryService;
        private readonly MetricsProtoServiceClient _grpcClient;
        private readonly IClusterConfiguration _clusterConfiguration;
        private readonly ICustomGrpcClientFactory _customGrpcClientFactory;
        public MetricsService(ILogger logger,
            INodeMetadata nodeMetaData,
            IEntityDiscoveryService entityDiscoveryService,
            IRuntimeOperationIdProvider runtimeOperationIdProvider,
            IMetricAggregatorFactory metricAggregatorFactory,
            IClusterConfiguration clusterConfiguration,
            ICustomGrpcClientFactory customGrpcClientFactory)
        {
            _logger = logger;
            _entityDiscoveryService = entityDiscoveryService;
            _runtimeOperationIdProvider = runtimeOperationIdProvider;
            _metricAggregatorFactory = metricAggregatorFactory;
            _nodeMetaData = nodeMetaData;
            _clusterConfiguration = clusterConfiguration;
            _customGrpcClientFactory = customGrpcClientFactory;

            if (_nodeMetaData.NodeType != Nodes.NodeType.Master)
            {
                _grpcClient = _customGrpcClientFactory.GetClient<GrpcMetricsClient>(_clusterConfiguration.MasterNodeIP);
            }
        }

        public async ValueTask<bool> TryIncreaseConnectionsCountAsync(Guid requestId, CancellationToken token)
        {
            bool? updated = null;

            if (_nodeMetaData.NodeType != Nodes.NodeType.Master)
            {
                var response = await _grpcClient.UpdateConnectionsAsync(new UpdateConnectionsRequest
                {
                    RequestId = requestId.ToString(),
                    Increase = true
                });
                updated = response.Success;
            }
            else
            {
                requestId = await DiscoverRequestIdOnMaster(requestId, token);
            }

            if (requestId == Guid.Empty)
            {
                await _logger.LogAsync(_runtimeOperationIdProvider.OperationId, $"Failed to increase connections count because the requestId was empty", LPSLoggingLevel.Warning, token);
                updated ??= false;
                return updated.Value;
            }
            await QueryMetricsAsync(requestId, token);
            var throughputMetrics = _aggregators[requestId.ToString()]
                .OfType<IThroughputMetricCollector>();

            foreach (var metric in throughputMetrics)
            {
                await metric.IncreaseConnectionsCount(token);
            }
            updated ??= true;
            return updated.Value;
        }

        public async ValueTask<bool> TryDecreaseConnectionsCountAsync(Guid requestId, CancellationToken token)
        {
            bool? updated = null;
            if (_nodeMetaData.NodeType != Nodes.NodeType.Master)
            {
                var response = await _grpcClient.UpdateConnectionsAsync(new UpdateConnectionsRequest
                {
                    RequestId = requestId.ToString(),
                    Increase = false,
                });
                updated = response.Success;
            }
            else
            {
                requestId = await DiscoverRequestIdOnMaster(requestId, token);
            }
            if (requestId == Guid.Empty)
            {
                await _logger.LogAsync(_runtimeOperationIdProvider.OperationId, $"Failed to decrease connections count because the requestId was empty", LPSLoggingLevel.Warning, token);
                updated ??= false;
                return updated.Value;
            }
            await QueryMetricsAsync(requestId, token);
            var throughputMetrics = _aggregators[requestId.ToString()]
                .OfType<IThroughputMetricCollector>();
            foreach (var metric in throughputMetrics)
            {
                await metric.DecreseConnectionsCount(token);
            }
            updated ??= true;
            return updated.Value;
        }

        public async ValueTask<bool> TryUpdateResponseMetricsAsync(Guid requestId, HttpResponse.SetupCommand lpsResponse, CancellationToken token)
        {
            bool? updated = null;
            if (_nodeMetaData.NodeType != Nodes.NodeType.Master)
            {
                var response = await _grpcClient.UpdateResponseMetricsAsync(new UpdateResponseMetricsRequest
                {
                    RequestId = requestId.ToString(),
                    ResponseCode = (int)lpsResponse.StatusCode,
                    StatusReason = lpsResponse.StatusMessage,
                });
                updated = response.Success;
            }
            else
            {
                requestId = await DiscoverRequestIdOnMaster(requestId, token);
            }

            if (requestId == Guid.Empty)
            {
                await _logger.LogAsync(_runtimeOperationIdProvider.OperationId, $"Failed to update response metrics because the requestId was empty", LPSLoggingLevel.Warning, token);
                updated ??= false;
                return updated.Value;
            }
            await QueryMetricsAsync(requestId, token);
            var responseMetrics = _aggregators[requestId.ToString()]
                .OfType<IResponseMetricCollector>();
           
            var result = await Task.WhenAll(responseMetrics.Select(metric => metric.UpdateAsync(lpsResponse, token)));
            updated ??= true;
            return updated.Value;
        }

        public async ValueTask<bool> TryUpdateDataSentAsync(Guid requestId, double totalBytes, CancellationToken token)
        {
            bool? updated = null;
            if (_nodeMetaData.NodeType != Nodes.NodeType.Master)
            {
                var response = await _grpcClient.UpdateDataTransmissionAsync(new UpdateDataTransmissionRequest
                {
                    RequestId = requestId.ToString(),
                    DataSize = totalBytes,
                    IsSent = true
                });
                updated = response.Success;
            }
            else
            {
                requestId = await DiscoverRequestIdOnMaster(requestId, token);
            }
            if (requestId == Guid.Empty)
            {
                await _logger.LogAsync(_runtimeOperationIdProvider.OperationId, $"Failed to update data sent because the requestId was empty", LPSLoggingLevel.Warning, token);
                updated ??= false;
                return updated.Value;
            }
            var dataTransmissionMetrics = await GetDataTransmissionMetricsAsync(requestId, token);
            foreach (var metric in dataTransmissionMetrics)
            {
                await metric.UpdateDataSentAsync(totalBytes, token);
            }
            updated ??= true;
            return updated.Value;
        }

        public async ValueTask<bool> TryUpdateDataReceivedAsync(Guid requestId, double totalBytes, CancellationToken token)
        {
            bool? updated = null;

            if (_nodeMetaData.NodeType == Nodes.NodeType.Worker)
            {
                var response = await _grpcClient.UpdateDataTransmissionAsync(new UpdateDataTransmissionRequest
                {
                    RequestId = requestId.ToString(),
                    DataSize = totalBytes,
                    IsSent = false
                });
                updated = response.Success;
            }
            else
            {
                requestId = await DiscoverRequestIdOnMaster(requestId, token);
            }
            if (requestId == Guid.Empty)
            {
                await _logger.LogAsync(_runtimeOperationIdProvider.OperationId, $"Failed to update data received because the requestId was empty", LPSLoggingLevel.Warning, token);
                updated ??= false;
                return updated.Value;
            }
            var dataTransmissionMetrics = await GetDataTransmissionMetricsAsync(requestId, token);
            foreach (var metric in dataTransmissionMetrics)
            {
                await metric.UpdateDataReceivedAsync(totalBytes, token);
            }
            updated ??= false;
            return updated.Value;
        }
        public async ValueTask<bool> TryUpdateDurationMetricAsync(Guid requestId, DurationMetricType metricType, double valueMs, CancellationToken token)
        {
            
            bool? updated = null;
            if (_nodeMetaData.NodeType != Nodes.NodeType.Master)
            {
                var response = await _grpcClient.UpdateDurationMetricAsync(new UpdateDurationMetricRequest
                {
                    RequestId = requestId.ToString(),
                    MetricType = metricType switch
                    {
                        DurationMetricType.TotalTime => ProtoDurationMetricType.TotalTime,
                        DurationMetricType.ReceivingTime => ProtoDurationMetricType.ReceivingTime,   // RENAMED
                        DurationMetricType.SendingTime => ProtoDurationMetricType.SendingTime,       // RENAMED
                        DurationMetricType.TLSHandshakeTime => ProtoDurationMetricType.TlsHandshakeTime,
                        DurationMetricType.TCPHandshakeTime => ProtoDurationMetricType.TcpHandshakeTime,
                        DurationMetricType.TimeToFirstByte => ProtoDurationMetricType.TimeToFirstByte,
                        DurationMetricType.WaitingTime => ProtoDurationMetricType.WaitingTime,       // NEW
                        _ => ProtoDurationMetricType.TotalTime
                    },
                    ValueMs = valueMs
                });
                updated = response.Success;
            }
            else
            {
                requestId = await DiscoverRequestIdOnMaster(requestId, token);
            }

            if (requestId == Guid.Empty)
            {
                await _logger.LogAsync(_runtimeOperationIdProvider.OperationId, $"Failed to update duration metrics because the requestId was empty", LPSLoggingLevel.Warning, token);
                updated ??= false;
                return updated.Value;
            }
            await QueryMetricsAsync(requestId, token);

            if (!_aggregators.TryGetValue(requestId.ToString(), out var metrics))
            {
                updated ??= false;
                return updated.Value;
            }
            var durationCollectors = metrics
                .Where(metric => metric.MetricType == LPSMetricType.Time)
                .OfType<IDurationMetricCollector>()
                .ToList();
            if (durationCollectors.Count == 0)
            {
                updated ??= false;
                return updated.Value;
            }

            foreach (var collector in durationCollectors)
            {
                switch (metricType)
                {
                    case DurationMetricType.TotalTime:
                        await collector.UpdateTotalTimeAsync(valueMs, token);
                        break;
                    case DurationMetricType.ReceivingTime: // RENAMED
                        await collector.UpdateReceivingTimeAsync(valueMs, token);
                        break;
                    case DurationMetricType.SendingTime:   // RENAMED
                        await collector.UpdateSendingTimeAsync(valueMs, token);
                        break;
                    case DurationMetricType.TLSHandshakeTime:
                        await collector.UpdateTLSHandshakeTimeAsync(valueMs, token);
                        break;
                    case DurationMetricType.TCPHandshakeTime:
                        await collector.UpdateTCPHandshakeTimeAsync(valueMs, token);
                        break;
                    case DurationMetricType.TimeToFirstByte:
                        await collector.UpdateTimeToFirstByteAsync(valueMs, token);
                        break;
                    case DurationMetricType.WaitingTime: // NEW
                        await collector.UpdateWaitingTimeAsync(valueMs, token);
                        break;
                }
            }

            updated ??= true;
            return updated.Value;
        }

        private async ValueTask<IEnumerable<IDataTransmissionMetricAggregator>> GetDataTransmissionMetricsAsync(Guid requestId, CancellationToken token)
        {
            await QueryMetricsAsync(requestId, token);
            return _aggregators[requestId.ToString()]
                .OfType<IDataTransmissionMetricAggregator>();
        }

        private async Task QueryMetricsAsync(Guid requestId, CancellationToken token) // call this so in case the request id was sent by the worker to be translated to the matching one on the master
        {
            var iteration = _metricAggregatorFactory.Iterations.Single(iteration => iteration.HttpRequest.Id == requestId);
            _metricAggregatorFactory.TryGet(iteration.Id, out IReadOnlyList<IMetricAggregator> aggregators);
            _aggregators.TryAdd(requestId.ToString(), aggregators);
        }
        private async Task<Guid> DiscoverRequestIdOnMaster(Guid requestId, CancellationToken token)
        {
            var entityDiscoveryRecord = _entityDiscoveryService.Discover(r => r.RequestId == requestId)?.SingleOrDefault();
            if (entityDiscoveryRecord != null)
            {
                // if this is worker, there is no need to translate as this is the local requestId
                if (_nodeMetaData.NodeType == Nodes.NodeType.Worker)
                {
                    return requestId;
                }

                // if this is master and the record does not belong to master, then get the matching on the master so the metrics are properly updated
                if (entityDiscoveryRecord.Node.Metadata.NodeType != Nodes.NodeType.Master)
                {
                    var fullyQualifiedName = entityDiscoveryRecord.FullyQualifiedName;
                    var record = _entityDiscoveryService.Discover(r => r.Node.Metadata.NodeType == Nodes.NodeType.Master && r.FullyQualifiedName == fullyQualifiedName)?.SingleOrDefault();
                    var matchingRequestId = record?.RequestId ?? Guid.Empty; // Empty means it was registered by the worker but was not defined in the plan executing on the master
                    if (record != null)
                    {
                        await _logger.LogAsync(_runtimeOperationIdProvider.OperationId, $"Found a matching HTTP request for '{requestId}' on the master node (ID: {matchingRequestId})", LPSLoggingLevel.Warning, token);
                    }
                    else
                    {
                        await _logger.LogAsync(_runtimeOperationIdProvider.OperationId, $"No matching HTTP request for '{requestId}' on the master node (ID: {matchingRequestId})", LPSLoggingLevel.Warning, token);
                    }
                    return matchingRequestId;
                }
                return requestId;
            }
            await _logger.LogAsync(_runtimeOperationIdProvider.OperationId, $"No matching HTTP request found for '{requestId}'", LPSLoggingLevel.Warning, token);
            return Guid.Empty;
        }
    }
}
