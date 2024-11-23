using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using LPS.Domain.Common.Interfaces;
using LPS.Domain.Domain.Common.Exceptions;
using LPS.Domain.LPSFlow.LPSHandlers;
using YamlDotNet.Serialization;

namespace LPS.Domain
{

    public partial class HttpRequest
    {
        new public class SetupCommand : ICommand<HttpRequest>, IValidCommand<HttpRequest>
        {
            public SetupCommand()
            {
                HttpVersion = "2.0";
                DownloadHtmlEmbeddedResources = false;
                SaveResponse = false;
                HttpHeaders = new Dictionary<string, string>();
                ValidationErrors = new Dictionary<string, List<string>>();
            }
            [JsonIgnore]
            [YamlIgnore]
            public Guid? Id { get; set; }
            [YamlMember(Alias = "url")]
            public string URL { get; set; }
            public string HttpMethod { get; set; }
            public string HttpVersion { get; set; }
            [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
            public Dictionary<string, string> HttpHeaders { get; set; }
            [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
            public string Payload { get; set; }
            public bool? DownloadHtmlEmbeddedResources { get; set; }
            public bool? SaveResponse { get; set; }
            [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
            public bool? SupportH2C { get; set; }

            [JsonIgnore]
            [YamlIgnore]
            public bool IsValid { get; set; }
            [JsonIgnore]
            [YamlIgnore]
            public IDictionary<string, List<string>> ValidationErrors { get; set; }
            public void Execute(HttpRequest entity)
            {
                ArgumentNullException.ThrowIfNull(entity);
                entity?.Setup(this);
            }

            public void Copy(SetupCommand targetCommand)
            {
                targetCommand.Id = this.Id;
                targetCommand.URL = this.URL;
                targetCommand.HttpMethod = this.HttpMethod;
                targetCommand.HttpVersion = this.HttpVersion;
                targetCommand.HttpHeaders = new Dictionary<string, string>(this.HttpHeaders);
                targetCommand.Payload = this.Payload;
                targetCommand.DownloadHtmlEmbeddedResources = this.DownloadHtmlEmbeddedResources;
                targetCommand.SaveResponse = this.SaveResponse;
                targetCommand.SupportH2C = this.SupportH2C;
                targetCommand.IsValid = this.IsValid;
                targetCommand.ValidationErrors = this.ValidationErrors?.ToDictionary(entry => entry.Key, entry => new List<string>(entry.Value));
            }
        }

        public void SetCapture(CaptureHandler capture) 
        {
            if (capture != null && capture.IsValid)
            {
                this.Capture = capture;
            }
            else
            {
                _logger.Log(_runtimeOperationIdProvider.OperationId, $"The referenced LPS Entity of type {typeof(CaptureHandler)} is either null or invalid.", LPSLoggingLevel.Error);
                throw new InvalidLPSEntityException($"The referenced LPS Entity of type {typeof(CaptureHandler)} is either null or invalid.");
            }
        }

        protected void Setup(SetupCommand command)
        {
            //Set the inherited properties through the parent entity setup command
            var requestSetUpCommand = new Request.SetupCommand() { Id = command.Id };
            base.Setup(requestSetUpCommand);
            //TODO: DeepCopy and then send the copy item instead of the original command for further protection 
            var validator = new Validator(this, command, _logger, _runtimeOperationIdProvider);
            if (command.IsValid && requestSetUpCommand.IsValid)
            {
                this.HttpMethod = command.HttpMethod;
                this.HttpVersion = command.HttpVersion;
                this.URL = command.URL;
                this.Payload = command.Payload;
                this.HttpHeaders = [];
                this.DownloadHtmlEmbeddedResources = command.DownloadHtmlEmbeddedResources.HasValue ? command.DownloadHtmlEmbeddedResources.Value : false;
                this.SaveResponse = command.SaveResponse.Value;
                this.SupportH2C = command.SupportH2C;
                if (command.HttpHeaders != null)
                {
                    foreach (var header in command.HttpHeaders)
                    {
                        this.HttpHeaders.Add(header.Key, header.Value);
                    }
                }

                this.IsValid = true;
            }
            else
            {
                this.IsValid = false;
                validator.PrintValidationErrors();
            }
        }

        public object Clone()
        {
            HttpRequest clone = new(_logger, _runtimeOperationIdProvider);
            if (this.IsValid)
            {
                clone.Id = this.Id;
                clone.HttpMethod = this.HttpMethod;
                clone.HttpVersion = this.HttpVersion;
                clone.URL = this.URL;
                clone.Payload = this.Payload;
                clone.DownloadHtmlEmbeddedResources = this.DownloadHtmlEmbeddedResources;
                clone.SaveResponse = this.SaveResponse;
                clone.SupportH2C = this.SupportH2C;
                clone.Capture =  (CaptureHandler)this.Capture?.Clone();
                clone.HttpHeaders = [];
                if (this.HttpHeaders != null)
                {
                    foreach (var header in this.HttpHeaders)
                    {
                        clone.HttpHeaders.Add(header.Key, header.Value);
                    }
                }
                clone.IsValid = true;
            }
            return clone;
        }
    }
}
