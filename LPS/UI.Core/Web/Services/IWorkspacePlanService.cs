using LPS.UI.Common.DTOs;
using LPS.UI.Core.Web.Models;

namespace LPS.UI.Core.Web.Services;

public interface IWorkspacePlanService
{
    Task<IReadOnlyList<WorkspacePlan>> ListAsync(CancellationToken token);
    Task<WorkspacePlan> GetAsync(Guid id, CancellationToken token);
    Task<WorkspacePlan> SaveAsync(Guid? id, PlanDto plan, CancellationToken token);
    Task DeleteAsync(Guid id, CancellationToken token);
}