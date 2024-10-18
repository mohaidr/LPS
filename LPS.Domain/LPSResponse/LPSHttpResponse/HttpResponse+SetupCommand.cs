using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using LPS.Domain.Common;
using LPS.Domain.Common.Interfaces;

namespace LPS.Domain
{

    public partial class HttpResponse
    {
        new public class SetupCommand : ICommand<HttpResponse>, IValidCommand<HttpResponse>
        {
            public SetupCommand()
            {
                ValidationErrors = new Dictionary<string, List<string>>();
            }
            [JsonIgnore]
            public Guid? Id { get; set; }

            public MimeType ContentType { get; set; }
            public string LocationToResponse { get; set; }
            public HttpStatusCode StatusCode { get; set; }
            public string StatusMessage { get; set; }
            public Dictionary<string, string> ResponseContentHeaders { get; set; }
            public Dictionary<string, string> ResponseHeaders { get; set; }
            public bool IsSuccessStatusCode { get; set; }
            [JsonIgnore]
            public bool IsValid { get; set; }
            public Guid LPSHttpRequestProfileId { get; set; }
            [JsonIgnore]
            public IDictionary<string, List<string>> ValidationErrors { get; set; }
            public TimeSpan ResponseTime { get; set; }

            public void Execute(HttpResponse entity)
            {
                ArgumentNullException.ThrowIfNull(entity);
                entity?.Setup(this);
            }
            public HttpRequestProfile.SetupCommand LPSHttpRequestProfile { get; set; }
        }

        protected void Setup(SetupCommand command)
        {
            //Set the inherited properties through the parent entity setup command
            var lPSResponseSetUpCommand = new Response.SetupCommand() { Id = command.Id };
            base.Setup(lPSResponseSetUpCommand);
            var validator = new Validator(this, command, _logger, _runtimeOperationIdProvider);
            if (command.IsValid && lPSResponseSetUpCommand.IsValid)
            {
                this.LocationToResponse = command.LocationToResponse;
                this.StatusCode = command.StatusCode;
                this.ContentType= command.ContentType;
                this.IsSuccessStatusCode= command.IsSuccessStatusCode;
                this.ResponseHeaders = new Dictionary<string, string>();
                this.ResponseContentHeaders = new Dictionary<string, string>();
                this.StatusMessage = command.StatusMessage;
                this.ResponseTime = command.ResponseTime;
                if (command.ResponseHeaders != null)
                {
                    foreach (var header in command.ResponseHeaders)
                    {
                        this.ResponseHeaders.Add(header.Key, header.Value);
                    }
                }
                if (command.ResponseContentHeaders != null)
                {
                    foreach (var header in command.ResponseContentHeaders)
                    {
                        this.ResponseContentHeaders.Add(header.Key, header.Value);
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
    }
}
