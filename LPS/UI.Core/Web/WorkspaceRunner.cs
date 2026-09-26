namespace LPS.UI.Core.Web;

internal static class WorkspaceRunner
{
    internal static bool IsManaged => !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("LPS_WORKSPACE_RUN"));

    internal static async Task ReportAsync(string state)
    {
        var directory = Environment.GetEnvironmentVariable("LPS_WORKSPACE_RUN");
        if (string.IsNullOrEmpty(directory))
            return;
        var path = Path.Combine(directory, state == "Failed" ? "failed" : "state");
        await File.WriteAllTextAsync($"{path}.tmp", state);
        File.Move($"{path}.tmp", path, overwrite: true);
        if (state == "Failed")
            Environment.ExitCode = 1;
    }
}