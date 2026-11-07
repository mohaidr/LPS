namespace LPS.UI.Core.Web.Models;

/// <summary>Starts a saved plan. Omit round names to run all rounds; otherwise select at least one existing round.</summary>
public sealed record StartWorkspaceRunRequest(Guid PlanId, IReadOnlyList<string>? RoundNames = null);