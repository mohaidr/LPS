using Grpc.Core;
using LPS.Domain.Common.Interfaces;
using LPS.Infrastructure.Common.Interfaces;
using LPS.Protos.Shared;
using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
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
            var success = request.Increase
                ? await _metricsService.TryIncreaseConnectionsCountAsync(Guid.Parse(request.RequestId), context.CancellationToken)
                : await _metricsService.TryDecreaseConnectionsCountAsync(Guid.Parse(request.RequestId), context.CancellationToken);
            await _logger.LogAsync(_runtimeOperationIdProvider.OperationId, $"Update connections count request completed successfully for {request.RequestId}", LPSLoggingLevel.Verbose, _cts.Token);

            return new UpdateConnectionsResponse { Success = success };
        }

        public override async Task<UpdateResponseMetricsResponse> UpdateResponseMetrics(UpdateResponseMetricsRequest request, ServerCallContext context)
        {
            var success = await _metricsService.TryUpdateResponseMetricsAsync(
                Guid.Parse(request.RequestId),
                new LPS.Domain.HttpResponse.SetupCommand { StatusCode = (HttpStatusCode)request.ResponseCode, StatusMessage = request.StatusReason },
                _cts.Token)  ;
            await _logger.LogAsync(_runtimeOperationIdProvider.OperationId, $"Update response metrics completed successfully for {request.RequestId}", LPSLoggingLevel.Verbose, _cts.Token);

            return new UpdateResponseMetricsResponse { Success = success };
        }

        public override async Task<UpdateDataTransmissionResponse> UpdateDataTransmission(UpdateDataTransmissionRequest request, ServerCallContext context)
        {

            var success = request.IsSent
                ? await _metricsService.TryUpdateDataSentAsync(Guid.Parse(request.RequestId), request.DataSize, _cts.Token)
                : await _metricsService.TryUpdateDataReceivedAsync(Guid.Parse(request.RequestId), request.DataSize, _cts.Token);
            await _logger.LogAsync(_runtimeOperationIdProvider.OperationId, $"Update data transmission metrics completed successfully for {request.RequestId}", LPSLoggingLevel.Verbose, _cts.Token);

            return new UpdateDataTransmissionResponse { Success = success };
        }
    }
}
