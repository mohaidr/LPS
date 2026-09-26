namespace LPS.UI.Core.Web.Models;

public sealed record WorkspaceHostStatus(string Application, Guid InstanceId, int ProcessId, string DataDirectory, bool IsStopping)
{
    public const string ApplicationName = "LPS.Workspace";
}