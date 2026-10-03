using System.Security.Cryptography;
using System.Text;
using LPS.Infrastructure.Distributed;
using Microsoft.AspNetCore.Http;

namespace Apis.Middleware;

public sealed class ClusterAccessMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var settings = ClusterRunSettings.Current!;
        if (context.Request.ContentType?.StartsWith("application/grpc", StringComparison.OrdinalIgnoreCase) == true)
        {
            var control = context.Request.Path.StartsWithSegments("/workers.WorkerControl");
            var expected = control ? settings.RegistrationToken : settings.Token;
            var supplied = context.Request.Headers.Authorization.ToString();
            if (string.IsNullOrEmpty(expected) || !CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(supplied), Encoding.UTF8.GetBytes($"Bearer {expected}")))
            {
                context.Response.StatusCode = StatusCodes.Status200OK;
                context.Response.ContentType = "application/grpc";
                context.Response.Headers["grpc-status"] = "16";
                context.Response.Headers["grpc-message"] = "Cluster authentication required";
                return;
            }
            if (context.Request.Path is var path && (path == "/nodes.NodeService/RegisterNode"
                || path == "/nodes.NodeService/TriggerTest" || path == "/nodes.NodeService/CancelTest"))
            {
                context.Response.ContentType = "application/grpc";
                context.Response.Headers["grpc-status"] = "12";
                return;
            }
        }
        else if (context.Connection.RemoteIpAddress is { } remote && !System.Net.IPAddress.IsLoopback(remote))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return;
        }
        await next(context);
    }
}