using System.Text.Json;

namespace LPS.UI.Core.Web.Models;

/// <summary>The last retained snapshot of an exported LPS metric.</summary>
public sealed record WorkspaceMetric(string Name, JsonElement Snapshot);