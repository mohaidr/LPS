using LPS.UI.Core.Web.Models;
using LPS.UI.Core.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace LPS.UI.Core.Web.Controllers;

[ApiController]
[Route("api/workspace/runs")]
[Produces("application/json")]
public sealed class WorkspaceRunsController(IWorkspaceRunService runs) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<WorkspaceRun>>> List(CancellationToken token)
        => Ok(await runs.ListAsync(token));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<WorkspaceRunDetails>> Get(Guid id, CancellationToken token)
        => Ok(await runs.GetAsync(id, token));

    [HttpPost]
    [ProducesResponseType(typeof(WorkspaceRun), 201)]
    public async Task<ActionResult<WorkspaceRun>> Start(StartWorkspaceRunRequest request, CancellationToken token)
    {
        var run = await runs.StartAsync(request.PlanId, token);
        return CreatedAtAction(nameof(Get), new { id = run.Id }, run);
    }

    [HttpPost("{id:guid}/stop")]
    [ProducesResponseType(typeof(WorkspaceRun), 202)]
    public async Task<ActionResult<WorkspaceRun>> Stop(Guid id, CancellationToken token)
        => AcceptedAtAction(nameof(Get), new { id }, await runs.StopAsync(id, token));
}