#nullable disable
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace LPS.UI.Core.Recording.Har
{
    // Minimal POCOs for the subset of the HAR 1.2 format that Playwright emits and that the
    // converter needs. Deserialized with System.Text.Json (see RecordCliCommand).

    internal sealed class HarRoot
    {
        [JsonPropertyName("log")]
        public HarLog Log { get; set; }
    }

    internal sealed class HarLog
    {
        [JsonPropertyName("entries")]
        public List<HarEntry> Entries { get; set; } = new();
    }

    internal sealed class HarEntry
    {
        // Playwright-specific field: "document" | "fetch" | "xhr" | "stylesheet" | "script" | "image" | "font" | ...
        [JsonPropertyName("_resourceType")]
        public string ResourceType { get; set; }

        [JsonPropertyName("request")]
        public HarRequest Request { get; set; }

        [JsonPropertyName("response")]
        public HarResponse Response { get; set; }
    }

    internal sealed class HarRequest
    {
        [JsonPropertyName("method")]
        public string Method { get; set; }

        [JsonPropertyName("url")]
        public string Url { get; set; }

        [JsonPropertyName("httpVersion")]
        public string HttpVersion { get; set; }

        [JsonPropertyName("headers")]
        public List<HarHeader> Headers { get; set; } = new();

        [JsonPropertyName("postData")]
        public HarPostData PostData { get; set; }
    }

    internal sealed class HarResponse
    {
        [JsonPropertyName("status")]
        public int Status { get; set; }

        [JsonPropertyName("headers")]
        public List<HarHeader> Headers { get; set; } = new();

        [JsonPropertyName("content")]
        public HarContent Content { get; set; }
    }

    internal sealed class HarHeader
    {
        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("value")]
        public string Value { get; set; }
    }

    internal sealed class HarPostData
    {
        [JsonPropertyName("mimeType")]
        public string MimeType { get; set; }

        [JsonPropertyName("text")]
        public string Text { get; set; }

        [JsonPropertyName("params")]
        public List<HarPostParam> Params { get; set; } = new();
    }

    internal sealed class HarPostParam
    {
        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("value")]
        public string Value { get; set; }

        [JsonPropertyName("fileName")]
        public string FileName { get; set; }

        [JsonPropertyName("contentType")]
        public string ContentType { get; set; }
    }

    internal sealed class HarContent
    {
        [JsonPropertyName("mimeType")]
        public string MimeType { get; set; }

        [JsonPropertyName("size")]
        public long Size { get; set; }
    }
}
