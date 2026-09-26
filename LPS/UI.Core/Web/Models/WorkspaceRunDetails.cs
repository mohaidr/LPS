namespace LPS.UI.Core.Web.Models;

/// <summary>A run with recent output and retained metric snapshots.</summary>
public sealed record WorkspaceRunDetails(WorkspaceRun Run, IReadOnlyList<string> Logs, IReadOnlyList<WorkspaceMetric> Metrics);