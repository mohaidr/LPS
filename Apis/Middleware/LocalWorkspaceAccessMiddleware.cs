using System.Net;
using Microsoft.AspNetCore.Http;

namespace Apis.Middleware;

public sealed class LocalWorkspaceAccessMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var host = context.Request.Host.Host;
        var localHost = string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase)
            || IPAddress.TryParse(host, out var address) && IPAddress.IsLoopback(address);
        var origin = context.Request.Headers.Origin.ToString();
        var localOrigin = string.IsNullOrEmpty(origin)
            || Uri.TryCreate(origin, UriKind.Absolute, out var uri) && uri.IsLoopback;
        var permittedApiRequest = !context.Request.Path.StartsWithSegments("/api/workspace")
            || context.Request.Headers["X-LPS-Workspace"] == "1";
        if (!localHost || !localOrigin || !permittedApiRequest)
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return;
        }
        await next(context);
    }
}