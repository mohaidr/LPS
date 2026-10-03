using System;
using System.IO;
using System.Text.Json;
using LPS.Infrastructure.Nodes;

namespace LPS.Infrastructure.Distributed;

public sealed record ClusterRunSettings
{
    public const string EnvironmentKey = "LPS_CLUSTER_RUN_SETTINGS";
    private static readonly Lazy<ClusterRunSettings?> Settings = new(Load);
    public static ClusterRunSettings? Current => Settings.Value;
    public required string RunId { get; init; }
    public DateTime StartedUtc { get; init; } = DateTime.UtcNow;
    public required string NodeName { get; init; }
    public required string RunDirectory { get; init; }
    public required string PlanPath { get; init; }
    public required string NodeAddress { get; init; }
    public required string MasterAddress { get; init; }
    public required NodeType Role { get; init; }
    public required string Token { get; init; }
    public string? RegistrationToken { get; init; }
    public string? CertificatePath { get; init; }
    public string? CertificatePassword { get; init; }
    public string? CertificateAuthorityPath { get; init; }
    public int ExpectedWorkers { get; init; } = 1;
    public bool MasterNodeIsWorker { get; init; }
    public int RegistrationTimeoutSeconds { get; init; } = 60;
    public int LeaseSeconds { get; init; } = 10;

    public static Uri ValidateEndpoint(string address)
    {
        if (!Uri.TryCreate(address, UriKind.Absolute, out var endpoint)
            || endpoint.Scheme is not ("http" or "https") || endpoint.AbsolutePath != "/"
            || endpoint.UserInfo.Length > 0 || endpoint.Query.Length > 0 || endpoint.Fragment.Length > 0)
            throw new ArgumentException("Use an absolute HTTP(S) node address with a port and no path, credentials, or query.");
        if (endpoint.Scheme == "http" && !endpoint.IsLoopback)
            throw new ArgumentException("Remote cluster connections require HTTPS. HTTP is allowed only on loopback.");
        return endpoint;
    }

    private static ClusterRunSettings? Load()
    {
        var path = Environment.GetEnvironmentVariable(EnvironmentKey);
        if (string.IsNullOrWhiteSpace(path)) return null;
        var settings = JsonSerializer.Deserialize<ClusterRunSettings>(File.ReadAllText(path))
            ?? throw new InvalidOperationException("Missing distributed run settings.");
        ValidateEndpoint(settings.NodeAddress);
        ValidateEndpoint(settings.MasterAddress);
        if (!Guid.TryParse(settings.RunId, out _) || settings.Token.Length < 32 || !Enum.IsDefined(settings.Role))
            throw new InvalidOperationException("Invalid distributed run identity or credentials.");
        return settings;
    }
}