// MetricsController.cs (refactored)
using LPS.Domain.Common.Interfaces;
using LPS.Infrastructure.Monitoring.MetricsServices;
using LPS.Domain.Domain.Common.Enums;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading;
using System.Threading.Tasks;
using LPS.Common.Interfaces;
using LPS.Infrastructure.Common.Interfaces;
using ILogger = LPS.Domain.Common.Interfaces.ILogger;

namespace Apis.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class MetricsController(
        ILogger logger,
        IRuntimeOperationIdProvider runtimeOperationIdProvider,
        IMetricsUiService metricsUiService,
        CancellationTokenSource cts) : ControllerBase
    {
        private readonly ILogger _logger = logger;
        private readonly IRuntimeOperationIdProvider _op = runtimeOperationIdProvider;
        private readonly IMetricsUiService _ui = metricsUiService;
        private readonly CancellationToken _token = cts.Token;

        /// <summary>
        /// GET /api/metrics?round=RoundA&iterationId={guid}&type=Throughput
        /// Any filter is optional; combine as needed.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> Get([FromQuery] string? round, [FromQuery] Guid? iterationId, [FromQuery] LPSMetricType? type)
        {
            var dto = await _ui.QueryAsync(new MetricsQuery(round, iterationId, type), _token);
            return Ok(dto);
        }

        /// <summary>GET /api/metrics/by-round/{roundName}</summary>
        [HttpGet("by-round/{roundName}")]
        public async Task<IActionResult> ByRound([FromRoute] string roundName)
            => Ok(await _ui.LatestForRoundAsync(roundName, _token));

        /// <summary>GET /api/metrics/by-iteration/{iterationId}</summary>
        [HttpGet("by-iteration/{iterationId:guid}")]
        public async Task<IActionResult> ByIteration([FromRoute] Guid iterationId)
            => Ok(await _ui.LatestForIterationAsync(iterationId, _token));

        /// <summary>GET /api/metrics/by-type/{metricType}</summary>
        [HttpGet("by-type/{metricType}")]
        public async Task<IActionResult> ByType([FromRoute] LPSMetricType metricType)
            => Ok(await _ui.LatestByMetricTypeAsync(metricType, _token));
    }
}
