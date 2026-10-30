using System.Text.Json;
using LPS.UI.Core.LPSValidators;
using LPS.UI.Core.Recording;
using LPS.UI.Core.Recording.Har;
using LPS.UI.Core.Web.Models;

namespace LPS.UI.Core.Web.Services;

/// <summary>
/// Turns an uploaded HAR capture into an unsaved plan for the Workspace editor. This is a thin
/// adapter: all of the actual filtering/conversion logic is the same code `lps record` uses
/// (<see cref="Recording.HarToPlanConverter"/> and <see cref="Recording.CaptureFilter"/>), so the
/// two stay in lockstep instead of drifting apart.
/// </summary>
public sealed class HarImportService : IHarImportService
{
    private static readonly JsonSerializerOptions HarJsonOptions = new() { PropertyNameCaseInsensitive = true };

    public async Task<ImportHarResult> ImportAsync(Stream harStream, HarImportOptions options, CancellationToken token)
    {
        HarRoot? har;
        try
        {
            har = await JsonSerializer.DeserializeAsync<HarRoot>(harStream, HarJsonOptions, token);
        }
        catch (JsonException ex)
        {
            throw new ArgumentException($"The uploaded file is not a valid HAR (JSON) capture: {ex.Message}");
        }

        var capturedCount = har?.Log?.Entries?.Count ?? 0;
        if (capturedCount == 0)
            throw new ArgumentException("The uploaded file has no HAR entries (Log.Entries is empty).");

        var filter = new CaptureFilter(
            ignoreContentTypes: options.IgnoreContentTypes,
            ignoreMethods: options.IgnoreMethods,
            onlyHosts: options.OnlyHosts,
            ignoreHosts: options.IgnoreHosts,
            ignorePaths: options.IgnorePaths,
            ignoreHeaders: options.IgnoreHeaders,
            minimalHeaders: options.MinimalHeaders);

        var shape = new RecordPlanOptions
        {
            RoundName = string.IsNullOrWhiteSpace(options.RoundName) ? "Main" : options.RoundName,
            NumberOfClients = options.NumberOfClients,
            ArrivalDelay = options.ArrivalDelay,
            RunInParallel = options.RunInParallel,
            RequestCount = options.RequestCount,
            Duration = options.Duration,
            ThinkTime = options.ThinkTime,
            MaxThinkTime = options.MaxThinkTimeMs
        };

        var planName = string.IsNullOrWhiteSpace(options.PlanName) ? "Imported HAR" : options.PlanName!;
        var conversion = new HarToPlanConverter().Convert(har!, filter, planName, shape);
        var importedRound = conversion.Plan.Rounds[0];
        var importedCount = importedRound.Iterations?.Count ?? 0;

        var warnings = new List<string>();
        if (importedCount == 0)
        {
            warnings.Add(
                $"Captured {capturedCount} request(s) but 0 remained after filtering. Loosen the host, method, or path filters and try again.");
        }

        // PlanValidator is the same validator the Workspace runs before saving any plan — surfacing
        // its errors as warnings here lets the user see and fix them before they even hit Save.
        var validation = new PlanValidator(conversion.Plan).Validate();
        warnings.AddRange(validation.Errors.Select(error => error.ErrorMessage));

        // Resolve each pending upload's generated placeholder path from the converted plan itself,
        // rather than recomputing it, so the UI always shows the exact value the plan contains.
        var fileUploads = conversion.FileUploads.Select(upload =>
        {
            var placeholderPath = importedRound.Iterations?
                .FirstOrDefault(iteration => iteration.Name == upload.IterationName)?
                .HttpRequest?.Payload?.Multipart?.Files?
                .FirstOrDefault(file => file.Name == upload.FieldName)?
                .Path ?? string.Empty;
            return new HarFileUploadPlaceholder(upload.IterationName, upload.FieldName, upload.OriginalFileName, placeholderPath);
        }).ToArray();

        if (fileUploads.Length > 0)
        {
            warnings.Add(
                $"{fileUploads.Length} file upload field(s) were captured without their file bytes; set a real local path for each before running.");
        }

        return new ImportHarResult(conversion.Plan, capturedCount, importedCount, fileUploads, warnings);
    }
}
