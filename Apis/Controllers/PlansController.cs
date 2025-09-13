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
    public class PlansController(IEntityRepositoryService entityRepo, CancellationTokenSource cts) : ControllerBase
    {
        private readonly IEntityRepositoryService _repo = entityRepo;
        private readonly CancellationToken _token = cts.Token;

        [HttpGet]
        public IActionResult GetAllPlans()
            => Ok(_repo.Query<Plan>());

        [HttpGet("{planId:guid}")]
        public IActionResult GetPlanById(Guid planId)
            => Ok(_repo.Get<Plan>(planId));
    }
}
