// IterationStatusesController.cs
using LPS.Domain;
using LPS.Domain.Common.Interfaces;
using LPS.Domain.Domain.Common.Enums;
using LPS.Domain.Domain.Common.Interfaces;
using LPS.Infrastructure.Entity;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Apis.Controllers
{
    [ApiController]
    [Route("api")]
    [Produces("application/json")]
    public class IterationStatusesController(
        IEntityRepositoryService repo,
        IIterationStatusMonitor statusMonitor,
        CancellationTokenSource cts) : ControllerBase
    {
        private readonly IEntityRepositoryService _repo = repo;
        private readonly IIterationStatusMonitor _statusMonitor = statusMonitor;
        private readonly CancellationToken _token = cts.Token;

        public class IterationStatusDto
        {
            public required Guid IterationId { get; set; }
            public required string IterationName { get; set; } = string.Empty;
            public required string Status { get; set; }
        }

        // GET /api/iterations/{iterationId}/status
        [HttpGet("iterations/{iterationId:guid}/status")]
        public async Task<IActionResult> GetIterationStatus([FromRoute] Guid iterationId)
        {
            var iter = _repo.Get<HttpIteration>(iterationId);
            if (iter is null) return NotFound();

            var status = await _statusMonitor.GetTerminalStatusAsync(iter, _token);
            return Ok(new IterationStatusDto
            {
                IterationId = iter.Id,
                IterationName = iter.Name,
                Status = status.ToString()
            });
        }

        // GET /api/rounds/{roundId:guid}/statuses
        [HttpGet("rounds/{roundId:guid}/statuses")]
        public async Task<IActionResult> GetStatusesByRound([FromRoute] Guid roundId)
        {
            var round = _repo.Get<Round>(roundId);
            if (round is null) return NotFound();

            var iters = round.GetReadOnlyIterations().OfType<HttpIteration>().ToList();
            var tasks = iters.Select(async i => new IterationStatusDto
            {
                IterationId = i.Id,
                IterationName = i.Name,
                Status = (await _statusMonitor.GetTerminalStatusAsync(i, _token)).ToString()
            });

            var result = await Task.WhenAll(tasks);
            return Ok(result);
        }
    }
}
