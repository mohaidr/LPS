using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using LPS.Domain.Common;
using LPS.Domain.Common.Interfaces;

namespace LPS.Domain
{

    public partial class LPSHttpResponse
    {
        new public class SetupCommand : ICommand<LPSHttpResponse>, IValidCommand<LPSHttpResponse>
        {
            public SetupCommand()
            {
                ValidationErrors = new Dictionary<string, List<string>>();
            }
            public Guid? Id { get; set; }

            public MimeType ContentType { get; set; }
            public string LocationToResponse { get; set; }
            public HttpStatusCode StatusCode { get; set; }
            public string StatusMessage { get; set; }
            public Dictionary<string, string> ResponseContentHeaders { get; set; }
            public Dictionary<string, string> ResponseHeaders { get; set; }
            public bool IsSuccessStatusCode { get; set; }
            public bool IsValid { get; set; }
            public Guid LPSHttpRequestProfileId { get; set; }
            public IDictionary<string, List<string>> ValidationErrors { get; set; }
            public TimeSpan ResponseTime { get; set; }

            public void Execute(LPSHttpResponse entity)
            {
                if (entity == null)
                {
                    throw new ArgumentNullException(nameof(entity));
                }
                entity?.Setup(this);
            }
            public LPSHttpRequestProfile.SetupCommand LPSHttpRequestProfile { get; set; }
        }

        protected void Setup(SetupCommand command)
        {
            //Set the inherited properties through the parent entity setup command
            var lPSResponseSetUpCommand = new LPSResponse.SetupCommand() { Id = command.Id };
            base.Setup(lPSResponseSetUpCommand);
            new Validator(this, command, _logger, _runtimeOperationIdProvider);
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
        }
    }
}
