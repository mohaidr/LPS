// IterationsController.cs
using AutoMapper;
using LPS.Domain;
using LPS.Domain.Common.Interfaces;
using Microsoft.AspNetCore.Mvc;
using LPS.Infrastructure.Entity;
using LPS.UI.Common.DTOs;

namespace Apis.Controllers
{
    [ApiController]
    [Route("api/iterations")]
    [Produces("application/json")]
    public class IterationsController(IEntityRepositoryService repo, IMapper mapper) : ControllerBase
    {
        private readonly IEntityRepositoryService _repo = repo;
        private readonly IMapper _mapper = mapper;

        // GET /api/iterations
        [HttpGet]
        public IActionResult GetAll()
            => Ok(_mapper.Map<IEnumerable<HttpIterationDto>>(
                _repo.Query<HttpIteration>().OfType<HttpIteration>()));

        // GET /api/iterations/{iterationId}
        [HttpGet("{iterationId:guid}")]
        public IActionResult GetById(Guid iterationId)
        {
            var iter = _repo.Get<HttpIteration>(iterationId);
            if (iter is null) return NotFound();

            if (iter is HttpIteration httpIter)
                return Ok(_mapper.Map<HttpIterationDto>(httpIter));

            return Problem("Iteration type is not supported by the current DTO mapping.");
        }

        // GET /api/rounds/{roundId}/iterations   (nested resource)
        [HttpGet("~/api/rounds/{roundId:guid}/iterations")]
        public IActionResult GetByRoundId(Guid roundId)
        {
            var round = _repo.Get<Round>(roundId);
            return round is null
                ? NotFound()
                : Ok(_mapper.Map<IEnumerable<HttpIterationDto>>(
                    round.GetReadOnlyIterations().OfType<HttpIteration>()));
        }
    }
}
