// PlansController.cs
using AutoMapper;
using LPS.Domain;
using LPS.Domain.Common.Interfaces;
using Microsoft.AspNetCore.Mvc;
using LPS.Infrastructure.Entity;
using LPS.UI.Common.DTOs;

namespace Apis.Controllers
{
    [ApiController]
    [Route("api/plans")]
    [Produces("application/json")]
    public class PlansController(IEntityRepositoryService repo, IMapper mapper) : ControllerBase
    {
        private readonly IEntityRepositoryService _repo = repo;
        private readonly IMapper _mapper = mapper;

        // GET /api/plans
        [HttpGet]
        public IActionResult GetAll()
            => Ok(_mapper.Map<IEnumerable<PlanDto>>(_repo.Query<Plan>()));

        // GET /api/plans/{planId}
        [HttpGet("{planId:guid}")]
        public IActionResult GetById(Guid planId)
        {
            var plan = _repo.Get<Plan>(planId);
            return plan is null ? NotFound() : Ok(_mapper.Map<PlanDto>(plan));
        }
    }
}