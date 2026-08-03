#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using LPS.Domain.LPSSession;
using LPS.UI.Common.DTOs;
using LPS.UI.Core.Recording.Har;

namespace LPS.UI.Core.Recording
{
    /// <summary>
    /// Turns a parsed HAR capture into a <see cref="PlanDto"/>: each kept entry becomes one
    /// <see cref="HttpIterationDto"/>, all grouped into a single round. The output is a starting
    /// draft — correlation and secret extraction are follow-ups. Multipart file parts are emitted as
    /// <see cref="FileFieldDto"/> placeholders whose path the user supplies (see <see cref="PendingFileUpload"/>).
    /// </summary>
    internal sealed class HarToPlanConverter
    {
        // Hop-by-hop / auto-managed headers that should not be replayed verbatim.
        private static readonly HashSet<string> SkipHeaders = new(StringComparer.OrdinalIgnoreCase)
        {
            "host", "content-length", "connection", "accept-encoding", "keep-alive",
            "proxy-connection", "transfer-encoding", "upgrade"
        };

        public ConversionResult Convert(HarRoot har, CaptureFilter filter, string planName, RecordPlanOptions? shape = null)
        {
            shape ??= new RecordPlanOptions();

            var iterations = new List<HttpIterationDto>();
            var fileUploads = new List<PendingFileUpload>();
            var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            var entries = har?.Log?.Entries ?? new List<HarEntry>();
            DateTimeOffset? previousKeptEnd = null;
            foreach (var entry in entries)
            {
                if (!filter.ShouldKeep(entry))
                    continue;

                var request = entry.Request;
                var name = UniqueName(BuildName(request.Method, request.Url), usedNames);

                var iteration = new HttpIterationDto
                {
                    Name = name,
                    HttpRequest = new HttpRequestDto
                    {
                        URL = request.Url ?? string.Empty,
                        HttpMethod = request.Method.ToUpperInvariant(),
                        HttpHeaders = BuildHeaders(request.Headers, filter),
                        Payload = BuildPayload(request, name, fileUploads)
                    }
                };
                ApplyIterationShape(iteration, shape);
                ApplyThinkTime(iteration, entry, shape, ref previousKeptEnd);
                iterations.Add(iteration);
            }

            var round = new RoundDto
            {
                Name = string.IsNullOrWhiteSpace(shape.RoundName) ? "Main" : shape.RoundName,
                Iterations = iterations
            };
            ApplyRoundShape(round, shape);

            var plan = new PlanDto
            {
                Name = string.IsNullOrWhiteSpace(planName) ? "RecordedPlan" : planName,
                Rounds = new List<RoundDto> { round }
            };

            return new ConversionResult { Plan = plan, FileUploads = fileUploads };
        }

        private static void ApplyRoundShape(RoundDto round, RecordPlanOptions shape)
        {
            round.NumberOfClients = string.IsNullOrWhiteSpace(shape.NumberOfClients) ? "1" : shape.NumberOfClients;

            if (!string.IsNullOrWhiteSpace(shape.ArrivalDelay))
                round.ArrivalDelay = shape.ArrivalDelay;

            if (shape.RunInParallel)
                round.RunInParallel = "true";
        }

        private static void ApplyIterationShape(HttpIterationDto iteration, RecordPlanOptions shape)
        {
            // A duration switches the iteration to duration mode; otherwise it stays request-count mode.
            if (!string.IsNullOrWhiteSpace(shape.Duration))
            {
                iteration.Mode = "D";
                iteration.Duration = shape.Duration;
            }
            else
            {
                iteration.Mode = "R";
                iteration.RequestCount = string.IsNullOrWhiteSpace(shape.RequestCount) ? "1" : shape.RequestCount;
            }
        }

        // startupDelay = idle gap since the previous kept request finished, so a sequential replay
        // paces itself like the recorded session. First request keeps no delay.
        private static void ApplyThinkTime(
            HttpIterationDto iteration, HarEntry entry, RecordPlanOptions shape, ref DateTimeOffset? previousKeptEnd)
        {
            if (!shape.ThinkTime)
                return;

            var start = entry.StartedDateTime;

            if (previousKeptEnd.HasValue)
            {
                var gapMs = (start - previousKeptEnd.Value).TotalMilliseconds;
                if (gapMs < 0)
                    gapMs = 0;
                if (shape.MaxThinkTime.HasValue && gapMs > shape.MaxThinkTime.Value)
                    gapMs = shape.MaxThinkTime.Value;
                if (gapMs >= 1)
                    iteration.StartupDelay = ((long)gapMs).ToString();
            }

            previousKeptEnd = start.AddMilliseconds(entry.Time > 0 ? entry.Time : 0);
        }

        private static Dictionary<string, string> BuildHeaders(List<HarHeader> headers, CaptureFilter filter)
        {
            var result = new Dictionary<string, string>();
            if (headers is null)
                return result;

            foreach (var header in headers)
            {
                if (string.IsNullOrEmpty(header?.Name))
                    continue;

                // Drop HTTP/2 pseudo-headers (":authority", ":method", ...) and hop-by-hop headers.
                if (header.Name.StartsWith(':'))
                    continue;
                if (SkipHeaders.Contains(header.Name))
                    continue;
                if (filter.ShouldDropHeader(header.Name))
                    continue;

                result[header.Name] = header.Value ?? string.Empty;
            }

            return result;
        }

        private static PayloadDto? BuildPayload(HarRequest request, string iterationName, List<PendingFileUpload> fileUploads)
        {
            var postData = request.PostData;
            if (postData is null)
                return null;

            var mimeType = !string.IsNullOrEmpty(postData.MimeType)
                ? postData.MimeType
                : GetContentTypeHeader(request);

            // multipart/form-data -> structured fields; file parts become user-supplied path placeholders.
            if (mimeType != null
                && mimeType.Contains("multipart/form-data", StringComparison.OrdinalIgnoreCase)
                && postData.Params is { Count: > 0 })
            {
                return BuildMultipart(postData.Params, iterationName, fileUploads);
            }

            // Everything else (json / text / x-www-form-urlencoded) is captured as a raw body.
            return string.IsNullOrEmpty(postData.Text)
                ? null
                : new PayloadDto { Type = Payload.PayloadType.Raw, Raw = postData.Text };
        }

        private static PayloadDto BuildMultipart(List<HarPostParam> parameters, string iterationName, List<PendingFileUpload> fileUploads)
        {
            var fields = new List<TextFieldDto>();
            var files = new List<FileFieldDto>();

            foreach (var part in parameters)
            {
                if (part is null || string.IsNullOrEmpty(part.Name))
                    continue;

                if (!string.IsNullOrEmpty(part.FileName))
                {
                    // We do not capture uploaded bytes; the user supplies a real path (placeholder is a hint).
                    var fileField = new FileFieldDto
                    {
                        Name = part.Name,
                        ContentType = part.ContentType ?? string.Empty,
                        Path = "files/" + SanitizeFileName(part.FileName)
                    };
                    files.Add(fileField);
                    fileUploads.Add(new PendingFileUpload
                    {
                        IterationName = iterationName,
                        FieldName = part.Name,
                        OriginalFileName = part.FileName,
                        SetPath = value => fileField.Path = value
                    });
                }
                else
                {
                    fields.Add(new TextFieldDto
                    {
                        Name = part.Name,
                        Value = part.Value ?? string.Empty,
                        ContentType = part.ContentType ?? string.Empty
                    });
                }
            }

            return new PayloadDto
            {
                Type = Payload.PayloadType.Multipart,
                Multipart = new MultipartContentDto { Fields = fields, Files = files }
            };
        }

        private static string? GetContentTypeHeader(HarRequest request)
        {
            return request.Headers?
                .FirstOrDefault(h => string.Equals(h?.Name, "content-type", StringComparison.OrdinalIgnoreCase))?
                .Value;
        }

        private static string SanitizeFileName(string fileName)
        {
            var name = (fileName ?? string.Empty).Replace('\\', '/');
            var slash = name.LastIndexOf('/');
            if (slash >= 0)
                name = name.Substring(slash + 1);
            return string.IsNullOrWhiteSpace(name) ? "upload" : name;
        }

        private static string BuildName(string method, string url)
        {
            var segment = "request";

            if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
            {
                var parts = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
                segment = parts.Length == 0
                    ? uri.Host
                    // Keep the parent segment for context: "/products/1" -> "products_1".
                    : string.Join('_', parts.Skip(Math.Max(0, parts.Length - 2)));
            }

            var clean = new string((segment ?? "request")
                .Select(c => char.IsLetterOrDigit(c) || c == '_' || c == '-' ? c : '_')
                .ToArray())
                .Trim('_');

            if (string.IsNullOrEmpty(clean))
                clean = "request";

            return $"{method.ToUpperInvariant()}_{clean}";
        }

        private static string UniqueName(string name, HashSet<string> used)
        {
            if (used.Add(name))
                return name;

            var index = 2;
            string candidate;
            do
            {
                candidate = $"{name}_{index++}";
            }
            while (!used.Add(candidate));

            return candidate;
        }
    }

    /// <summary>Result of converting a HAR capture: the plan plus any file uploads awaiting a real path.</summary>
    internal sealed class ConversionResult
    {
        public PlanDto Plan { get; init; } = new();
        public List<PendingFileUpload> FileUploads { get; init; } = new();
    }

    /// <summary>A captured multipart file part whose bytes are not available; the user supplies its path.</summary>
    internal sealed class PendingFileUpload
    {
        public string IterationName { get; init; } = string.Empty;
        public string FieldName { get; init; } = string.Empty;
        public string OriginalFileName { get; init; } = string.Empty;
        public Action<string> SetPath { get; init; } = _ => { };
    }

    /// <summary>Load-shape values a user can pass to `lps record` so the generated plan starts with their numbers.</summary>
    internal sealed class RecordPlanOptions
    {
        public string? RoundName { get; init; }
        public string? NumberOfClients { get; init; }
        public string? ArrivalDelay { get; init; }
        public bool RunInParallel { get; init; }
        public string? RequestCount { get; init; }
        public string? Duration { get; init; }
        public bool ThinkTime { get; init; }
        public int? MaxThinkTime { get; init; }
    }
}
