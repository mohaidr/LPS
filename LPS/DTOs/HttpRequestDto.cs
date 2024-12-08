using LPS.Domain;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using YamlDotNet.Serialization;

namespace LPS.DTOs
{
    public class HttpRequestDto : IDto<HttpRequestDto>
    {
        public HttpRequestDto()
        {
            HttpHeaders = [];
            HttpVersion = "2.0";
        }

        // URL for the HTTP request (supports placeholders)
        [YamlMember(Alias = "url")]
        public string URL { get; set; }

        // HTTP method (supports placeholders)
        public string HttpMethod { get; set; }

        // HTTP version (supports placeholders)
        public string HttpVersion { get; set; }

        // HTTP headers
        public Dictionary<string, string> HttpHeaders { get; set; }

        // Payload for the request (supports placeholders)
        public string Payload { get; set; }

        // Whether to download HTML embedded resources (supports placeholders)
        public string DownloadHtmlEmbeddedResources { get; set; }

        // Whether to save the response (supports placeholders)
        public string SaveResponse { get; set; }

        // Whether to support H2C (HTTP/2 over cleartext) (supports placeholders)
        public string SupportH2C { get; set; }

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
                Payload = this.Payload,
                DownloadHtmlEmbeddedResources = this.DownloadHtmlEmbeddedResources,
                SaveResponse = this.SaveResponse,
                SupportH2C = this.SupportH2C,
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
