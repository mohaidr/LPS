using LPS.UI.Core.Web.Models;
using LPS.UI.Core.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace LPS.UI.Core.Web.Controllers;

[ApiController]
[Route("api/workspace/settings")]
[Produces("application/json")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class WorkspaceSettingsController(IWorkspaceSettingsService settings) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(WorkspaceSettingsResponse), 200)]
    public async Task<ActionResult<WorkspaceSettingsResponse>> Get(CancellationToken token)
        => Ok(await settings.GetAsync(token));

    [HttpPut]
    [ProducesResponseType(typeof(WorkspaceSettingsResponse), 200)]
    [ProducesResponseType(typeof(ValidationProblemDetails), 400)]
    public async Task<ActionResult<WorkspaceSettingsResponse>> Update(SaveWorkspaceSettingsRequest request, CancellationToken token)
        => Ok(await settings.SaveAsync(request.Settings, token));
}