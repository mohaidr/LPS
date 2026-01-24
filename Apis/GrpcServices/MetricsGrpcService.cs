using Grpc.Core;
using LPS.Domain.Common.Interfaces;
using LPS.Infrastructure.Common.Interfaces;
using LPS.Protos.Shared;
using LPS.Infrastructure.Monitoring.Metrics;
using ProtoDurationMetricType = LPS.Protos.Shared.DurationMetricType;
using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using DurationMetricType = LPS.Infrastructure.Monitoring.Metrics.DurationMetricType;
namespace Apis.Services
{
    public class MetricsGrpcService : MetricsProtoService.MetricsProtoServiceBase
    {
        private readonly LPS.Domain.Common.Interfaces.ILogger _logger;
        private readonly IRuntimeOperationIdProvider _runtimeOperationIdProvider;
        private readonly IMetricsService _metricsService;
        private readonly CancellationTokenSource _cts;

        public MetricsGrpcService(LPS.Domain.Common.Interfaces.ILogger logger,
                                  IRuntimeOperationIdProvider runtimeOperationIdProvider,
                                  IMetricsService metricsService,
                                  CancellationTokenSource cts)
        {
            _logger = logger;
            _runtimeOperationIdProvider = runtimeOperationIdProvider;
            _metricsService = metricsService;
            _cts = cts;
        }

        public override async Task<UpdateConnectionsResponse> UpdateConnections(UpdateConnectionsRequest request, ServerCallContext context)
        {
            try
            {
                var success = request.Increase
                    ? await _metricsService.TryIncreaseConnectionsCountAsync(Guid.Parse(request.RequestId), context.CancellationToken)
                    : await _metricsService.TryDecreaseConnectionsCountAsync(Guid.Parse(request.RequestId), context.CancellationToken);
                await _logger.LogAsync(_runtimeOperationIdProvider.OperationId, $"Update connections count request completed successfully for {request.RequestId}", LPSLoggingLevel.Verbose, _cts.Token);

                return new UpdateConnectionsResponse { Success = success };
            }
            catch (OperationCanceledException ex)
            {
                await _logger.LogAsync(_runtimeOperationIdProvider.OperationId, $"UpdateConnections cancelled for {request.RequestId}: {ex.Message}", LPSLoggingLevel.Warning, CancellationToken.None);
                return new UpdateConnectionsResponse { Success = false };
            }
        }

        public override async Task<UpdateResponseMetricsResponse> UpdateResponseMetrics(UpdateResponseMetricsRequest request, ServerCallContext context)
        {
            try
            {
                var success = await _metricsService.TryUpdateResponseMetricsAsync(
                    Guid.Parse(request.RequestId),
                    new LPS.Domain.HttpResponse.SetupCommand { StatusCode = (HttpStatusCode)request.ResponseCode, StatusMessage = request.StatusReason },
                    _cts.Token);
                await _logger.LogAsync(_runtimeOperationIdProvider.OperationId, $"Update response metrics completed successfully for {request.RequestId}", LPSLoggingLevel.Verbose, _cts.Token);

                return new UpdateResponseMetricsResponse { Success = success };
            }
            catch (OperationCanceledException ex)
            {
                await _logger.LogAsync(_runtimeOperationIdProvider.OperationId, $"UpdateResponseMetrics cancelled for {request.RequestId}: {ex.Message}", LPSLoggingLevel.Warning, CancellationToken.None);
                return new UpdateResponseMetricsResponse { Success = false };
            }
        }

        public override async Task<UpdateDataTransmissionResponse> UpdateDataTransmission(UpdateDataTransmissionRequest request, ServerCallContext context)
        {
            try
            {
                var success = request.IsSent
                    ? await _metricsService.TryUpdateDataSentAsync(Guid.Parse(request.RequestId), request.DataSize, _cts.Token)
                    : await _metricsService.TryUpdateDataReceivedAsync(Guid.Parse(request.RequestId), request.DataSize, _cts.Token);
                await _logger.LogAsync(_runtimeOperationIdProvider.OperationId, $"Update data transmission metrics completed successfully for {request.RequestId}", LPSLoggingLevel.Verbose, _cts.Token);

                return new UpdateDataTransmissionResponse { Success = success };
            }
            catch (OperationCanceledException ex)
            {
                await _logger.LogAsync(_runtimeOperationIdProvider.OperationId, $"UpdateDataTransmission cancelled for {request.RequestId}: {ex.Message}", LPSLoggingLevel.Warning, CancellationToken.None);
                return new UpdateDataTransmissionResponse { Success = false };
            }
        }

        public override async Task<UpdateDurationMetricResponse> UpdateDurationMetric(UpdateDurationMetricRequest request, ServerCallContext context)
        {
            try
            {
                var metricType = request.MetricType switch
                {
                    ProtoDurationMetricType.TotalTime => DurationMetricType.TotalTime,
                    ProtoDurationMetricType.ReceivingTime => DurationMetricType.ReceivingTime,     // RENAMED
                    ProtoDurationMetricType.SendingTime => DurationMetricType.SendingTime,         // RENAMED
                    ProtoDurationMetricType.TlsHandshakeTime => DurationMetricType.TLSHandshakeTime,
                    ProtoDurationMetricType.TcpHandshakeTime => DurationMetricType.TCPHandshakeTime,
                    ProtoDurationMetricType.TimeToFirstByte => DurationMetricType.TimeToFirstByte,
                    ProtoDurationMetricType.WaitingTime => DurationMetricType.WaitingTime,         // NEW
                    _ => DurationMetricType.TotalTime
                };

                var success = await _metricsService.TryUpdateDurationMetricAsync(
                    Guid.Parse(request.RequestId),
                    metricType,
                    request.ValueMs,
                    _cts.Token);

                await _logger.LogAsync(
                    _runtimeOperationIdProvider.OperationId,
                    $"Update duration metric completed successfully for {request.RequestId} ({metricType}, value: {request.ValueMs} ms)",
                    LPSLoggingLevel.Verbose,
                    _cts.Token);

                return new UpdateDurationMetricResponse { Success = success };
            }
            catch (OperationCanceledException ex)
            {
                await _logger.LogAsync(_runtimeOperationIdProvider.OperationId, $"UpdateDurationMetric cancelled for {request.RequestId}: {ex.Message}", LPSLoggingLevel.Warning, CancellationToken.None);
                return new UpdateDurationMetricResponse { Success = false };
            }
        }

    }
}
