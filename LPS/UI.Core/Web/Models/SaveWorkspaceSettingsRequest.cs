using System.ComponentModel.DataAnnotations;

namespace LPS.UI.Core.Web.Models;

/// <summary>Settings to validate and save. A null InfluxDB token retains its saved value.</summary>
public sealed record SaveWorkspaceSettingsRequest
{
    [Required]
    public required WorkspaceSettings Settings { get; init; }
}