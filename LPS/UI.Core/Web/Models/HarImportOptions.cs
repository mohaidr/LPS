namespace LPS.UI.Core.Web.Models;

/// <summary>
/// Filter and load-shape options for converting an uploaded HAR capture into a plan. Mirrors the
/// filtering/shaping options already exposed by `lps record` (see <c>CaptureFilter</c> and
/// <c>RecordPlanOptions</c> in <c>LPS.UI.Core.Recording</c>), adapted for a single web request.
/// </summary>
public sealed record HarImportOptions
{
    public string? PlanName { get; init; }
    public string? RoundName { get; init; }
    public string[] OnlyHosts { get; init; } = Array.Empty<string>();
    public string[] IgnoreHosts { get; init; } = Array.Empty<string>();
    public string[] IgnorePaths { get; init; } = Array.Empty<string>();
    public string[] IgnoreMethods { get; init; } = Array.Empty<string>();
    public string[] IgnoreContentTypes { get; init; } = Array.Empty<string>();
    public string[] IgnoreHeaders { get; init; } = Array.Empty<string>();
    public bool MinimalHeaders { get; init; }
    public bool ThinkTime { get; init; }
    public int? MaxThinkTimeMs { get; init; }
    public string? NumberOfClients { get; init; }
    public string? ArrivalDelay { get; init; }
    public bool RunInParallel { get; init; }
    public string? RequestCount { get; init; }
    public string? Duration { get; init; }
}
