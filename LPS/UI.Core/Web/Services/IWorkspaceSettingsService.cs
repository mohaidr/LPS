using LPS.UI.Core.Web.Models;

namespace LPS.UI.Core.Web.Services;

public interface IWorkspaceSettingsService
{
    Task<WorkspaceSettingsResponse> GetAsync(CancellationToken token);
    Task<WorkspaceSettingsResponse> SaveAsync(WorkspaceSettings settings, CancellationToken token);
}