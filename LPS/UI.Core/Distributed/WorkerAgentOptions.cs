namespace LPS.UI.Core.Distributed;

internal sealed class WorkerAgentOptions
{
    public required string Name { get; init; }
    public required string MasterAddress { get; init; }
    public required string ListenAddress { get; init; }
    public required string DataDirectory { get; init; }
    public required string SettingsPath { get; init; }
    public required string Token { get; init; }
    public string? CertificatePath { get; init; }
    public string? CertificatePassword { get; init; }
    public string? CertificateAuthorityPath { get; init; }
}