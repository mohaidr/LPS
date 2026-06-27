using LPS.Domain;
using LPS.Domain.LPSSession;
using LPS.Infrastructure.Common;
using LPS.Infrastructure.Common.LPSSerializer;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using YamlDotNet.Serialization;

namespace LPS.UI.Common.DTOs
{
    public class HttpRequestDto : DtoBase<HttpRequestDto>
    {
        #pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
        public HttpRequestDto()
        #pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
        {
            HttpHeaders = [];
            HttpVersion = "2.0";
            SupportH2C = "false";
            Retry = new RetryDto();
        }

        public class RetryDto
        {
            [YamlAlias("if")]
            [JsonAlias("if")]
            public string If { get; set; }

            [YamlAlias("stopIf")]
            [JsonAlias("stopIf")]
            public string StopIf { get; set; }

            [YamlAlias("maxRetries")]
            [JsonAlias("maxRetries")]
            public string MaxRetries { get; set; }

            [YamlAlias("baseDelayInMs")]
            [JsonAlias("baseDelayInMs")]
            public string BaseDelayInMs { get; set; }

            [YamlAlias("maxDelayInMs")]
            [JsonAlias("maxDelayInMs")]
            public string MaxDelayInMs { get; set; }
        }

        // URL for the HTTP request (supports placeholders)
        [YamlAlias("url")]
        [JsonAlias("url")]
        public string URL { get; set; }

        // HTTP method (supports placeholders)
        [YamlAlias("method")]
        [JsonAlias("method")]
        public string HttpMethod { get; set; }

        // HTTP version (supports placeholders)
        [YamlAlias("version")]
        [JsonAlias("version")]
        public string HttpVersion { get; set; }

        // Evaluator condition (can be a variable)
        public string SkipIf { get; set; }

        // Retry policy object (preferred).
        [YamlAlias("retry")]
        [JsonAlias("retry")]
        public RetryDto Retry { get; set; }

        // HTTP headers
        [YamlAlias("headers")]
        [JsonAlias("headers")]
        public Dictionary<string, string> HttpHeaders { get; set; }

        public PayloadDto Payload { get; set; }

        // Whether to download HTML embedded resources (supports placeholders)
        public string DownloadHtmlEmbeddedResources { get; set; }

        // Whether to save the response (supports placeholders)
        public string SaveResponse { get; set; }

        // Whether to support H2C (HTTP/2 over cleartext) (supports placeholders)
        public string SupportH2C { get; set; }

        // Path to client certificate file (PFX/PEM) for mutual TLS authentication
        [YamlAlias("clientCertificatePath")]
        [JsonAlias("clientCertificatePath")]
        public string? ClientCertificatePath { get; set; }

        // Password for the client certificate file (required for PFX files)
        [YamlAlias("clientCertificatePassword")]
        [JsonAlias("clientCertificatePassword")]
        public string? ClientCertificatePassword { get; set; }

        // Capture handler for the HTTP request
        public CaptureHandlerDto Capture { get; set; }

        // Deep copy method
        public void DeepCopy(out HttpRequestDto targetDto)
        {
            #pragma warning disable CS8601 // Possible null reference assignment.
            targetDto = new HttpRequestDto
            {
                URL = this.URL,
                HttpMethod = this.HttpMethod,
                HttpVersion = this.HttpVersion,
                SkipIf = this.SkipIf,
                Retry = this.Retry == null
                    ? null
                    : new RetryDto
                    {
                        If = this.Retry.If,
                        StopIf = this.Retry.StopIf,
                        MaxRetries = this.Retry.MaxRetries,
                        BaseDelayInMs = this.Retry.BaseDelayInMs,
                        MaxDelayInMs = this.Retry.MaxDelayInMs
                    },
                Payload = this.Payload.CloneObject(),
                DownloadHtmlEmbeddedResources = this.DownloadHtmlEmbeddedResources,
                SaveResponse = this.SaveResponse,
                SupportH2C = this.SupportH2C,
                ClientCertificatePath = this.ClientCertificatePath,
                ClientCertificatePassword = this.ClientCertificatePassword,
                HttpHeaders = this.HttpHeaders?.ToDictionary(entry => entry.Key, entry => entry.Value)
            };
            #pragma warning restore CS8601 // Possible null reference assignment.
            // Deep copy the Capture object if it exists
            if (this.Capture != null)
            {
                var copiedCapture = new CaptureHandlerDto();
                this.Capture.DeepCopy(out copiedCapture);
                targetDto.Capture = copiedCapture;
            }
        }
    }
}
