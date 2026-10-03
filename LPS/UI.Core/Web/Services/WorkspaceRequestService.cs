using System.Diagnostics;
using LPS.Domain.LPSSession;
using LPS.Infrastructure.LPSClients;
using LPS.Infrastructure.LPSClients.HeaderServices;
using LPS.Infrastructure.LPSClients.MessageServices;
using LPS.UI.Common.DTOs;
using LPS.UI.Core.Web.Models;

namespace LPS.UI.Core.Web.Services;

public sealed class WorkspaceRequestService(HttpClient client, IWorkspaceSettingsService settings) : IWorkspaceRequestService
{
    private const int MaximumBodyBytes = 1024 * 1024;

    public async Task<WorkspaceRequestResult> SendAsync(HttpRequestDto request, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!Uri.TryCreate(request.URL, UriKind.Absolute, out var uri) || (uri.Scheme != "http" && uri.Scheme != "https"))
            throw new ArgumentException("Enter an absolute HTTP or HTTPS request URL.");
        var method = request.HttpMethod?.ToUpperInvariant();
        if (method is not ("GET" or "POST" or "PUT" or "PATCH" or "DELETE" or "HEAD" or "OPTIONS"))
            throw new ArgumentException("Choose a supported HTTP method.");
        if (request.HttpVersion is not ("1.1" or "2.0"))
            throw new ArgumentException("Choose HTTP 1.1 or 2.0.");
        if (!string.IsNullOrWhiteSpace(request.ClientCertificatePath))
            throw new ArgumentException("Test API does not support client certificate files.");

        if (MessageService.SupportsContent(method) && request.Payload?.Type is not (null or Payload.PayloadType.Raw))
            throw new ArgumentException("Test API supports raw request bodies only.");
        using var message = MessageService.CreateRequestMessage(request.URL, method, request.HttpVersion,
            string.Equals(request.SupportH2C, "true", StringComparison.OrdinalIgnoreCase),
            request.Payload == null ? null : request.Payload.Raw ?? "");
        var httpSettings = (await settings.GetAsync(token)).Settings.HttpClient;
        var defaults = HttpClientConfiguration.GetDefaultInstance();
        try
        {
            foreach (var header in request.HttpHeaders ?? [])
                HttpHeadersService.ApplyHeader(message, header.Key, header.Value,
                    httpSettings?.HeaderValidationMode ?? defaults.HeaderMode,
                    httpSettings?.AllowHostOverride ?? defaults.AllowHostOverride);
        }
        catch (NotSupportedException exception)
        {
            throw new ArgumentException(exception.Message, exception);
        }

        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(token);
        cancellation.CancelAfter(client.Timeout);
        var started = Stopwatch.GetTimestamp();
        try
        {
            using var response = await client.SendAsync(message, HttpCompletionOption.ResponseHeadersRead, cancellation.Token);
            await using var stream = await response.Content.ReadAsStreamAsync(cancellation.Token);
            var buffer = new byte[MaximumBodyBytes + 1];
            var received = 0;
            while (received < buffer.Length)
            {
                var count = await stream.ReadAsync(buffer.AsMemory(received), cancellation.Token);
                if (count == 0) break;
                received += count;
            }
            var length = Math.Min(received, MaximumBodyBytes);
            var contentType = response.Content.Headers.ContentType;
            var mediaType = contentType?.MediaType;
            var text = mediaType == null || mediaType.StartsWith("text/", StringComparison.OrdinalIgnoreCase)
                || mediaType.Contains("json", StringComparison.OrdinalIgnoreCase)
                || mediaType.Contains("xml", StringComparison.OrdinalIgnoreCase)
                || mediaType is "application/javascript" or "application/x-www-form-urlencoded";
            using var content = new ByteArrayContent(buffer, 0, length);
            content.Headers.ContentType = contentType;
            var body = text ? await content.ReadAsStringAsync(cancellation.Token) : Convert.ToBase64String(buffer, 0, length);
            var headers = response.Headers.Concat(response.Content.Headers)
                .GroupBy(header => header.Key, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.SelectMany(header => header.Value).ToArray(), StringComparer.OrdinalIgnoreCase);
            return new WorkspaceRequestResult((int)response.StatusCode, response.ReasonPhrase ?? "", response.Version.ToString(),
                headers, body, text ? "text" : "base64", length, received > MaximumBodyBytes, Stopwatch.GetElapsedTime(started).TotalMilliseconds);
        }
        catch (OperationCanceledException) when (!token.IsCancellationRequested)
        {
            throw new TimeoutException("The API request timed out.");
        }
    }
}