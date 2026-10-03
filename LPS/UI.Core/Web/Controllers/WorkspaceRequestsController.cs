using LPS.UI.Common.DTOs;
using LPS.UI.Core.Web.Models;
using LPS.UI.Core.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace LPS.UI.Core.Web.Controllers;

[ApiController]
[Route("api/workspace/requests")]
[Produces("application/json")]
public sealed class WorkspaceRequestsController(IWorkspaceRequestService requests) : ControllerBase
{
    /// <summary>Sends one draft HTTP request without saving a plan or starting a load run.</summary>
    [HttpPost("send")]
    [ProducesResponseType(typeof(WorkspaceRequestResult), 200)]
    [ProducesResponseType(typeof(ProblemDetails), 400)]
    [ProducesResponseType(typeof(ProblemDetails), 502)]
    [ProducesResponseType(typeof(ProblemDetails), 504)]
    public async Task<ActionResult<WorkspaceRequestResult>> Send(HttpRequestDto request, CancellationToken token)
        => Ok(await requests.SendAsync(request, token));
}