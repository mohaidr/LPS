using LPS.UI.Common.DTOs;
using LPS.UI.Core.Web.Models;

namespace LPS.UI.Core.Web.Services;

public interface IWorkspaceRequestService
{
    Task<WorkspaceRequestResult> SendAsync(HttpRequestDto request, CancellationToken token);
}