using LPS.UI.Core.Web.Models;

namespace LPS.UI.Core.Web.Services;

public interface IHarImportService
{
    /// <summary>Converts an uploaded HAR capture (as JSON) into an unsaved plan.</summary>
    Task<ImportHarResult> ImportAsync(Stream harStream, HarImportOptions options, CancellationToken token);
}
