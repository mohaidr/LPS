using LPS.UI.Core.Web.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;

namespace LPS.UI.Core.Web.Controllers;

[ApiController]
[Route("api/workspace/host")]
[Produces("application/json")]
public sealed class WorkspaceHostController(WorkspaceOptions options, IHostApplicationLifetime lifetime) : ControllerBase
{
    private static readonly Guid InstanceId = Guid.NewGuid();

    [HttpGet]
    [ProducesResponseType(typeof(WorkspaceHostStatus), 200)]
    public ActionResult<WorkspaceHostStatus> Get() => Ok(new WorkspaceHostStatus(
        WorkspaceHostStatus.ApplicationName, InstanceId, Environment.ProcessId,
        Path.GetFullPath(options.DataDirectory), lifetime.ApplicationStopping.IsCancellationRequested));

    [HttpPost("stop")]
    [ProducesResponseType(202)]
    [ProducesResponseType(typeof(ProblemDetails), 409)]
    public IActionResult Stop([FromQuery] Guid instanceId)
    {
        if (instanceId != InstanceId)
            return Conflict(new ProblemDetails { Status = 409, Title = "The UI host changed. Retry the stop command." });
        Response.OnCompleted(() =>
        {
            lifetime.StopApplication();
            return Task.CompletedTask;
        });
        return Accepted();
    }
}