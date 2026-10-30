using Microsoft.AspNetCore.Http;

namespace LPS.UI.Core.Web.Models;

/// <summary>
/// Multipart form submission for importing a HAR capture. Filter lists are comma-separated,
/// mirroring the equivalent `lps record` options (--only-host, --ignore-host, ...).
/// </summary>
public sealed class ImportHarRequest
{
    public IFormFile? Har { get; set; }
    public string? PlanName { get; set; }
    public string? RoundName { get; set; }
    public string? OnlyHosts { get; set; }
    public string? IgnoreHosts { get; set; }
    public string? IgnorePaths { get; set; }
    public string? IgnoreMethods { get; set; }
    public string? IgnoreContentTypes { get; set; }
    public string? IgnoreHeaders { get; set; }
    public bool MinimalHeaders { get; set; }
    public bool ThinkTime { get; set; }
    public int? MaxThinkTimeMs { get; set; }
    public string? NumberOfClients { get; set; }
    public string? ArrivalDelay { get; set; }
    public bool RunInParallel { get; set; }
    public string? RequestCount { get; set; }
    public string? Duration { get; set; }
}
