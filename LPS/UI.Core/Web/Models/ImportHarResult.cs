using LPS.UI.Common.DTOs;

namespace LPS.UI.Core.Web.Models;

/// <summary>
/// The outcome of converting an uploaded HAR capture: an unsaved <see cref="PlanDto"/> plus anything
/// the user should review before running it (filtered-out requests, validation notes, file uploads
/// that need a real local path).
/// </summary>
public sealed record ImportHarResult(
    PlanDto Plan,
    int CapturedRequestCount,
    int ImportedRequestCount,
    IReadOnlyList<HarFileUploadPlaceholder> FileUploads,
    IReadOnlyList<string> Warnings);

/// <summary>
/// A multipart file field captured without its bytes (HAR captures never include uploaded file
/// contents). <see cref="PlaceholderPath"/> is the generated placeholder and needs a real local path
/// before the request can run.
/// </summary>
public sealed record HarFileUploadPlaceholder(string IterationName, string FieldName, string OriginalFileName, string PlaceholderPath);
