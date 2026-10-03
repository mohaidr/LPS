namespace LPS.UI.Core.Web.Models;

/// <summary>The bounded response from a single workspace API request.</summary>
public sealed record WorkspaceRequestResult(
    int StatusCode,
    string ReasonPhrase,
    string HttpVersion,
    IReadOnlyDictionary<string, string[]> Headers,
    string Body,
    string BodyEncoding,
    int BodyBytes,
    bool Truncated,
    double ElapsedMilliseconds);