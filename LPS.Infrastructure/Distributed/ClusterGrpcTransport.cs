using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using Grpc.Net.Client;

namespace LPS.Infrastructure.Distributed;

public static class ClusterGrpcTransport
{
    public static GrpcChannel CreateChannel(string address, GrpcChannelOptions? options = null, string? token = null, string? certificateAuthorityPath = null)
    {
        var settings = ClusterRunSettings.Current;
        token ??= settings?.Token;
        certificateAuthorityPath ??= settings?.CertificateAuthorityPath;
        if (token == null) return GrpcChannel.ForAddress(address, options ?? new GrpcChannelOptions());
        ClusterRunSettings.ValidateEndpoint(address);
        var handler = new HttpClientHandler();
        if (!string.IsNullOrWhiteSpace(certificateAuthorityPath))
        {
            var authorityPem = System.IO.File.ReadAllText(certificateAuthorityPath);
            handler.ServerCertificateCustomValidationCallback = (_, certificate, _, errors) =>
            {
                if (certificate == null || (errors & SslPolicyErrors.RemoteCertificateNameMismatch) != 0) return false;
                using var certificateAuthority = X509Certificate2.CreateFromPem(authorityPem);
                using var chain = new X509Chain();
                chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
                chain.ChainPolicy.CustomTrustStore.Add(certificateAuthority);
                chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
                chain.ChainPolicy.ApplicationPolicy.Add(new System.Security.Cryptography.Oid("1.3.6.1.5.5.7.3.1"));
                return chain.Build(certificate);
            };
        }
        var client = new HttpClient(handler);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        options ??= new GrpcChannelOptions();
        options.HttpClient = client;
        options.DisposeHttpClient = true;
        options.MaxReceiveMessageSize = 4 * 1024 * 1024;
        return GrpcChannel.ForAddress(address, options);
    }
}