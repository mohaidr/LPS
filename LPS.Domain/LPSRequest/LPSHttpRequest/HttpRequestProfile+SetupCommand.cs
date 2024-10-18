using System;
using System.Collections.Generic;
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

namespace LPS.Domain
{

    public partial class HttpRequestProfile
    {
        new public class SetupCommand : ICommand<HttpRequestProfile>, IValidCommand<HttpRequestProfile>
        {
            public SetupCommand()
            {
                Httpversion = "2.0";
                DownloadHtmlEmbeddedResources = false;
                SaveResponse = false;
                HttpHeaders = new Dictionary<string, string>();
                ValidationErrors = new Dictionary<string, List<string>>();
            }
            [JsonIgnore]
            public Guid? Id { get; set; }

            public string HttpMethod { get; set; }
            public string URL { get; set; }

            public string Payload { get; set; }

            public string Httpversion { get; set; }

            public Dictionary<string, string> HttpHeaders { get; set; }
            public bool? DownloadHtmlEmbeddedResources { get; set; }
            public bool? SaveResponse { get; set; }

            [JsonIgnore]
            public bool IsValid { get; set; }
            [JsonIgnore]
            public IDictionary<string, List<string>> ValidationErrors { get; set; }
            public void Execute(HttpRequestProfile entity)
            {
                ArgumentNullException.ThrowIfNull(entity);
                entity?.Setup(this);
            }

        }

        protected void Setup(SetupCommand command)
        {

            //Set the inherited properties through the parent entity setup command
            var lPSRequestProfileSetUpCommand = new RequestProfile.SetupCommand() { Id = command.Id };
            base.Setup(lPSRequestProfileSetUpCommand);
            var validator = new Validator(this, command, _logger, _runtimeOperationIdProvider);
            if (command.IsValid && lPSRequestProfileSetUpCommand.IsValid)
            {
                this.HttpMethod = command.HttpMethod;
                this.Httpversion = command.Httpversion;
                this.URL = command.URL;
                this.Payload = command.Payload;
                this.HttpHeaders = new Dictionary<string, string>();
                this.DownloadHtmlEmbeddedResources = command.DownloadHtmlEmbeddedResources.HasValue? command.DownloadHtmlEmbeddedResources.Value: false;
                this.SaveResponse = command.SaveResponse.Value;
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
            HttpRequestProfile clone = new HttpRequestProfile(_logger, _runtimeOperationIdProvider);
            if (this.IsValid)
            {
                clone.Id = this.Id;
                clone.HttpMethod = this.HttpMethod;
                clone.Httpversion = this.Httpversion;
                clone.URL = this.URL;
                clone.Payload = this.Payload;
                clone.DownloadHtmlEmbeddedResources = this.DownloadHtmlEmbeddedResources;
                clone.SaveResponse = this.SaveResponse;
                clone.HttpHeaders = new Dictionary<string, string>();
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
