#nullable enable
using System;

namespace LPS.Infrastructure.Monitoring.Hosts
{
    public readonly record struct HostKey(string Scheme, string Host, int Port)
    {
        public static HostKey From(Uri targetUri)
        {
            ArgumentNullException.ThrowIfNull(targetUri);

            if (!targetUri.IsAbsoluteUri)
                throw new ArgumentException("The target URI must be absolute.", nameof(targetUri));

            return new HostKey(
                targetUri.Scheme.ToLowerInvariant(),
                targetUri.IdnHost.ToLowerInvariant(),
                targetUri.Port);
        }

        public override string ToString() => $"{Scheme}://{Host}:{Port}";
    }
}