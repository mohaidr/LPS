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
using LPS.Domain;
using LPS.Infrastructure.Entity;

namespace Apis.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class IterationsController(IEntityRepositoryService entityRepo, CancellationTokenSource cts) : ControllerBase
    {
        private readonly IEntityRepositoryService _repo = entityRepo;
        private readonly CancellationToken _token = cts.Token;

        [HttpGet]
        public IActionResult GetAllIterations()
            => Ok(_repo.Query<HttpIteration>());

        [HttpGet("/api/rounds/{roundId:guid}/iterations")]
        public IActionResult GetIterationsByRound(Guid roundId)
        {
            var round = _repo.Get<Round>(roundId);
            return round is not null ? Ok(round.GetReadOnlyIterations()) : NotFound();
        }

        [HttpGet("{iterationId:guid}")]
        public IActionResult GetIterationById(Guid iterationId)
            => Ok(_repo.Get<HttpIteration>(iterationId));
    }
}
