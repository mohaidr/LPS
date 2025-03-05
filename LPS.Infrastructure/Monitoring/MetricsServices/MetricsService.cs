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
using Metrics;
using static Metrics.MetricsProtoService;

namespace LPS.Infrastructure.Monitoring.MetricsServices
{
    public class MetricsService : IMetricsService
    {
        private readonly ILogger _logger;
        private readonly IRuntimeOperationIdProvider _runtimeOperationIdProvider;
        private readonly ConcurrentDictionary<string, IList<IMetricCollector>> _metrics = new();
        private readonly IMetricsQueryService _metricsQueryService;
        private readonly INodeMetadata _nodeMetaData;
        private readonly MetricsProtoServiceClient _grpcClient;

        public MetricsService(ILogger logger,
            INodeMetadata nodeMetaData,
            IRuntimeOperationIdProvider runtimeOperationIdProvider,
            IMetricsQueryService metricsQueryService)
        {
            _logger = logger;
            _runtimeOperationIdProvider = runtimeOperationIdProvider;
            _metricsQueryService = metricsQueryService;
            _nodeMetaData = nodeMetaData;

            if (_nodeMetaData.NodeType != NodeType.Master)
            {
                var channel = GrpcChannel.ForAddress("https://your-grpc-server");
                _grpcClient = new MetricsProtoServiceClient(channel);
            }
        }

        public async ValueTask<bool> TryIncreaseConnectionsCountAsync(Guid requestId, CancellationToken token)
        {
            if (_nodeMetaData.NodeType != NodeType.Master)
            {
                var response = await _grpcClient.UpdateConnectionsAsync(new UpdateConnectionsRequest
                {
                    RequestId = requestId.ToString(),
                    Increase = true
                });
                return response.Success;
            }
            await QueryMetricsAsync(requestId);
            var throughputMetrics = _metrics[requestId.ToString()]
                .Where(metric => metric.MetricType == LPSMetricType.Throughput);
            foreach (var metric in throughputMetrics)
            {
                ((IThroughputMetricCollector)metric).IncreaseConnectionsCount();
            }
            return true;
        }

        public async ValueTask<bool> TryDecreaseConnectionsCountAsync(Guid requestId, bool isSuccessful, CancellationToken token)
        {
            if (_nodeMetaData.NodeType != NodeType.Master)
            {
                var response = await _grpcClient.UpdateConnectionsAsync(new UpdateConnectionsRequest
                {
                    RequestId = requestId.ToString(),
                    Increase = false,
                    IsSuccessful = isSuccessful
                });
                return response.Success;
            }
            await QueryMetricsAsync(requestId);
            var throughputMetrics = _metrics[requestId.ToString()]
                .Where(metric => metric.MetricType == LPSMetricType.Throughput);
            foreach (var metric in throughputMetrics)
            {
                ((IThroughputMetricCollector)metric).DecreseConnectionsCount(isSuccessful);
            }
            return true;
        }

        public async ValueTask<bool> TryUpdateResponseMetricsAsync(Guid requestId, HttpResponse.SetupCommand lpsResponse, CancellationToken token)
        {
            if (_nodeMetaData.NodeType != NodeType.Master)
            {
                var response = await _grpcClient.UpdateResponseMetricsAsync(new UpdateResponseMetricsRequest
                {
                    RequestId = requestId.ToString(),
                    ResponseCode = (int)lpsResponse.StatusCode,
                    ResponseTime = Google.Protobuf.WellKnownTypes.Duration.FromTimeSpan(lpsResponse.TotalTime)
                });
                return response.Success;
            }
            await QueryMetricsAsync(requestId);
            var responseMetrics = _metrics[requestId.ToString()]
                .Where(metric => metric.MetricType == LPSMetricType.ResponseTime || metric.MetricType == LPSMetricType.ResponseCode);
            await Task.WhenAll(responseMetrics.Select(metric => ((IResponseMetricCollector)metric).UpdateAsync(lpsResponse)));
            return true;
        }

        public async ValueTask<bool> TryUpdateDataSentAsync(Guid requestId, double dataSize, double uploadTime, CancellationToken token)
        {
            if (_nodeMetaData.NodeType != NodeType.Master)
            {
                var response = await _grpcClient.UpdateDataTransmissionAsync(new UpdateDataTransmissionRequest
                {
                    RequestId = requestId.ToString(),
                    DataSize = dataSize,
                    TimeTaken = uploadTime,
                    IsSent = true
                });
                return response.Success;
            }
            var dataTransmissionMetrics = await GetDataTransmissionMetricsAsync(requestId);
            foreach (var metric in dataTransmissionMetrics)
            {
                ((IDataTransmissionMetricCollector)metric).UpdateDataSent(dataSize, uploadTime, token);
            }
            return true;
        }

        public async ValueTask<bool> TryUpdateDataReceivedAsync(Guid requestId, double dataSize, double downloadTime, CancellationToken token)
        {
            if (_nodeMetaData.NodeType != NodeType.Master)
            {
                var response = await _grpcClient.UpdateDataTransmissionAsync(new UpdateDataTransmissionRequest
                {
                    RequestId = requestId.ToString(),
                    DataSize = dataSize,
                    TimeTaken = downloadTime,
                    IsSent = false
                });
                return response.Success;
            }
            var dataTransmissionMetrics = await GetDataTransmissionMetricsAsync(requestId);
            foreach (var metric in dataTransmissionMetrics)
            {
                ((IDataTransmissionMetricCollector)metric).UpdateDataReceived(dataSize, downloadTime, token);
            }
            return true;
        }

        private async ValueTask<IEnumerable<IMetricCollector>> GetDataTransmissionMetricsAsync(Guid requestId)
        {
            await QueryMetricsAsync(requestId);
            return _metrics[requestId.ToString()]
                .Where(metric => metric.MetricType == LPSMetricType.DataTransmission);
        }

        private async Task QueryMetricsAsync(Guid requestId)
        {
            _metrics.TryAdd(requestId.ToString(),
                await _metricsQueryService.GetAsync(metric => metric.HttpIteration.HttpRequest.Id == requestId));
        }
    }
}
