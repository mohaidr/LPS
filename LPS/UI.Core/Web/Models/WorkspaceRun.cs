using LPS.UI.Common.DTOs;

namespace LPS.UI.Core.Web.Models;

/// <summary>An immutable plan snapshot and the lifecycle of its isolated runner.</summary>
public sealed record WorkspaceRun(Guid Id, Guid PlanId, string Name, string State,
    DateTimeOffset StartedAt, DateTimeOffset? FinishedAt, int? ExitCode, int DashboardPort, PlanDto Plan);