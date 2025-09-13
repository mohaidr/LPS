// RoundsController.cs
using AutoMapper;
using LPS.Domain;
using LPS.Domain.Common.Interfaces;
using Microsoft.AspNetCore.Mvc;
using LPS.Infrastructure.Entity;
using LPS.UI.Common.DTOs;

namespace Apis.Controllers
{
    [ApiController]
    [Route("api/rounds")]
    [Produces("application/json")]
    public class RoundsController(IEntityRepositoryService repo, IMapper mapper) : ControllerBase
    {
        private readonly IEntityRepositoryService _repo = repo;
        private readonly IMapper _mapper = mapper;

        // GET /api/rounds
        [HttpGet]
        public IActionResult GetAll()
            => Ok(_mapper.Map<IEnumerable<RoundDto>>(_repo.Query<Round>()));

        // GET /api/rounds/{roundId}
        [HttpGet("{roundId:guid}")]
        public IActionResult GetById(Guid roundId)
        {
            var round = _repo.Get<Round>(roundId);
            return round is null ? NotFound() : Ok(_mapper.Map<RoundDto>(round));
        }

        // GET /api/plans/{planId}/rounds   (nested resource)
        [HttpGet("~/api/plans/{planId:guid}/rounds")]
        public IActionResult GetByPlanId(Guid planId)
        {
            var plan = _repo.Get<Plan>(planId);
            return plan is null
                ? NotFound()
                : Ok(_mapper.Map<IEnumerable<RoundDto>>(plan.GetReadOnlyRounds()));
        }
    }
}
