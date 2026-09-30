namespace LPS.UI.Core.Web.Models;

/// <summary>Settings and their source file, with the saved InfluxDB token omitted.</summary>
public sealed record WorkspaceSettingsResponse(string FilePath, WorkspaceSettings Settings, bool HasInfluxDBToken);