namespace LPS.UI.Core.Web.Models;

/// <summary>Starts a run from a saved workspace plan.</summary>
public sealed record StartWorkspaceRunRequest(Guid PlanId);