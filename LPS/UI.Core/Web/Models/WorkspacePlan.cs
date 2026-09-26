using LPS.UI.Common.DTOs;

namespace LPS.UI.Core.Web.Models;

/// <summary>A saved plan and its workspace identity.</summary>
public sealed record WorkspacePlan(Guid Id, DateTimeOffset UpdatedAt, PlanDto Plan);