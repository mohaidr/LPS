using LPS.Infrastructure.Common;
using LPS.UI.Core.Web.Models;
using LPS.UI.Core.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace LPS.UI.Core.Web.Controllers;

[ApiController]
[Route("api/workspace/plans")]
[Produces("application/json")]
public sealed class WorkspacePlansController(IWorkspacePlanService plans) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<WorkspacePlan>>> List(CancellationToken token)
        => Ok(await plans.ListAsync(token));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<WorkspacePlan>> Get(Guid id, CancellationToken token)
        => Ok(await plans.GetAsync(id, token));

    [HttpPost]
    [ProducesResponseType(typeof(WorkspacePlan), 201)]
    public async Task<ActionResult<WorkspacePlan>> Create(SaveWorkspacePlanRequest request, CancellationToken token)
    {
        var saved = await plans.SaveAsync(null, request.Plan, token);
        return CreatedAtAction(nameof(Get), new { id = saved.Id }, saved);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<WorkspacePlan>> Update(Guid id, SaveWorkspacePlanRequest request, CancellationToken token)
        => Ok(await plans.SaveAsync(id, request.Plan, token));

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(204)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken token)
    {
        await plans.DeleteAsync(id, token);
        return NoContent();
    }

    [HttpGet("{id:guid}/export")]
    [Produces("application/json", "application/yaml")]
    [ProducesResponseType(typeof(FileContentResult), 200)]
    [ProducesResponseType(typeof(ProblemDetails), 400)]
    [ProducesResponseType(typeof(ProblemDetails), 404)]
    public async Task<IActionResult> Export(Guid id, CancellationToken token, [FromQuery] string format = "json")
    {
        format = format?.ToLowerInvariant() ?? "json";
        if (format is not ("json" or "yaml"))
            return Problem(statusCode: 400, title: "Unsupported export format", detail: "Use json or yaml.");
        var saved = await plans.GetAsync(id, token);
        var content = format == "yaml" ? SerializationHelper.SerializeToYaml(saved.Plan) : SerializationHelper.Serialize(saved.Plan);
        return File(System.Text.Encoding.UTF8.GetBytes(content),
            $"application/{format}", $"{saved.Plan.Name}.{format}");
    }
}