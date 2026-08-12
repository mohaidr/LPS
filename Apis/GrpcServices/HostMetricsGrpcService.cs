using Grpc.Core;
using LPS.Infrastructure.Monitoring.Hosts;
using LPS.Protos.Shared;
using System;
using System.Net;
using System.Threading.Tasks;
using ProtoHostKey = LPS.Protos.Shared.HostKey;
using ProtoDurationMetricType = LPS.Protos.Shared.DurationMetricType;
using HostKey = LPS.Infrastructure.Monitoring.Hosts.HostKey;
using DurationMetricType = LPS.Infrastructure.Monitoring.Metrics.DurationMetricType;

namespace Apis.Services
{
    // Master-side handler for host metrics forwarded by workers. Applies each event
    // to the local host aggregator pipeline via IHostMetricsService.
    public class HostMetricsGrpcService : HostMetricsProtoService.HostMetricsProtoServiceBase
    {
        private readonly IHostMetricsService _hostMetricsService;

        public HostMetricsGrpcService(IHostMetricsService hostMetricsService)
        {
            _hostMetricsService = hostMetricsService;
        }

        public override async Task<HostMetricsResponse> IncreaseConnections(HostIncreaseConnectionsRequest request, ServerCallContext context)
        {
            var requestId = Guid.TryParse(request.RequestId, out var id) ? id : Guid.Empty;
            await _hostMetricsService.IncreaseConnectionsCountAsync(FromProto(request.HostKey), requestId, context.CancellationToken);
            return new HostMetricsResponse { Success = true };
        }

        public override async Task<HostMetricsResponse> DecreaseConnections(HostDecreaseConnectionsRequest request, ServerCallContext context)
        {
            await _hostMetricsService.DecreaseConnectionsCountAsync(FromProto(request.HostKey), context.CancellationToken);
            return new HostMetricsResponse { Success = true };
        }

        public override async Task<HostMetricsResponse> IncreaseSkippedRequests(HostSkippedRequestsRequest request, ServerCallContext context)
        {
            await _hostMetricsService.IncreaseSkippedRequestsCountAsync(FromProto(request.HostKey), context.CancellationToken);
            return new HostMetricsResponse { Success = true };
        }

        public override async Task<HostMetricsResponse> UpdateResponse(HostUpdateResponseRequest request, ServerCallContext context)
        {
            var command = new LPS.Domain.HttpResponse.SetupCommand
            {
                StatusCode = (HttpStatusCode)request.StatusCode,
                StatusMessage = request.StatusReason,
                IsSuccessStatusCode = request.IsSuccess
            };
            await _hostMetricsService.UpdateResponseAsync(FromProto(request.HostKey), command, context.CancellationToken);
            return new HostMetricsResponse { Success = true };
        }

        public override async Task<HostMetricsResponse> UpdateDuration(HostUpdateDurationRequest request, ServerCallContext context)
        {
            await _hostMetricsService.UpdateDurationAsync(FromProto(request.HostKey), FromProto(request.MetricType), request.ValueMs, context.CancellationToken);
            return new HostMetricsResponse { Success = true };
        }

        public override async Task<HostMetricsResponse> UpdateDataTransmission(HostUpdateDataTransmissionRequest request, ServerCallContext context)
        {
            var hostKey = FromProto(request.HostKey);
            if (request.IsSent)
                await _hostMetricsService.UpdateDataSentAsync(hostKey, request.DataSize, context.CancellationToken);
            else
                await _hostMetricsService.UpdateDataReceivedAsync(hostKey, request.DataSize, context.CancellationToken);
            return new HostMetricsResponse { Success = true };
        }

        private static HostKey FromProto(ProtoHostKey proto) => new(proto.Scheme, proto.Host, proto.Port);

        private static DurationMetricType FromProto(ProtoDurationMetricType metricType) => metricType switch
        {
            ProtoDurationMetricType.TotalTime => DurationMetricType.TotalTime,
            ProtoDurationMetricType.ReceivingTime => DurationMetricType.ReceivingTime,
            ProtoDurationMetricType.SendingTime => DurationMetricType.SendingTime,
            ProtoDurationMetricType.TlsHandshakeTime => DurationMetricType.TLSHandshakeTime,
            ProtoDurationMetricType.TcpHandshakeTime => DurationMetricType.TCPHandshakeTime,
            ProtoDurationMetricType.TimeToFirstByte => DurationMetricType.TimeToFirstByte,
            ProtoDurationMetricType.WaitingTime => DurationMetricType.WaitingTime,
            ProtoDurationMetricType.ServerTime => DurationMetricType.ServerTime,
            ProtoDurationMetricType.ServerTimeDb => DurationMetricType.ServerTimeDB,
            ProtoDurationMetricType.ServerTimeCache => DurationMetricType.ServerTimeCache,
            ProtoDurationMetricType.ServerTimeApp => DurationMetricType.ServerTimeApp,
            _ => DurationMetricType.TotalTime
        };
    }
}
