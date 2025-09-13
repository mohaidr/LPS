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
    public class RoundsController(IEntityRepositoryService entityRepo, CancellationTokenSource cts) : ControllerBase
    {
        private readonly IEntityRepositoryService _repo = entityRepo;
        private readonly CancellationToken _token = cts.Token;

        [HttpGet]
        public IActionResult GetAllRounds()
            => Ok(_repo.Query<Round>());

        [HttpGet("/api/plans/{planId:guid}/rounds")]
        public IActionResult GetRoundsByPlan(Guid planId)
        {
            var plan = _repo.Get<Plan>(planId);
            return plan is not null ? Ok(plan.GetReadOnlyRounds()) : NotFound();
        }

        [HttpGet("{roundId:guid}")]
        public IActionResult GetRoundById(Guid roundId)
            => Ok(_repo.Get<Round>(roundId));
    }
}
