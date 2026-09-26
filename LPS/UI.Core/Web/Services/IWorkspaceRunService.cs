using LPS.UI.Core.Web.Models;

namespace LPS.UI.Core.Web.Services;

public interface IWorkspaceRunService
{
    Task<IReadOnlyList<WorkspaceRun>> ListAsync(CancellationToken token);
    Task<WorkspaceRunDetails> GetAsync(Guid id, CancellationToken token);
    Task<WorkspaceRun> StartAsync(Guid planId, CancellationToken token);
    Task<WorkspaceRun> StopAsync(Guid id, CancellationToken token);
}