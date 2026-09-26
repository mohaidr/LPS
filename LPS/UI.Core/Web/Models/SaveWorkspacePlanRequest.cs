using System.ComponentModel.DataAnnotations;
using LPS.UI.Common.DTOs;

namespace LPS.UI.Core.Web.Models;

/// <summary>A plan to validate and save in the local workspace.</summary>
public sealed record SaveWorkspacePlanRequest
{
    [Required]
    public required PlanDto Plan { get; init; }
}