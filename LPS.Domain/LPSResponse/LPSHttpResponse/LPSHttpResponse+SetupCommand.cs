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

namespace LPS.Domain
{

    public partial class LPSHttpResponse
    {
        new public class SetupCommand : ICommand<LPSHttpResponse>, IValidCommand
        {
            public SetupCommand()
            {

            }

            public MimeType MIMEType { get; set; }
            public string LocationToResponse { get; set; }
            public HttpStatusCode StatusCode { get; set; }
            public Dictionary<string, string> ResponseContentHeaders { get; set; }
            public Dictionary<string, string> ResponseHeaders { get; set; }
            public bool IsSuccessStatusCode { get; set; }
            public bool IsValid { get; set; }
            public Guid LPSHttpRequestProfileId { get; set; }
            public IDictionary<string, string> ValidationErrors { get; set; }

            public void Execute(LPSHttpResponse entity)
            {
                entity?.Setup(this);
            }

            internal LPSResponse.SetupCommand LPSResponseSetUpCommand;
        }

        protected void Setup(SetupCommand command)
        {
            //Set the inherited properties through the parent entity setup command
            command.LPSResponseSetUpCommand = new LPSResponse.SetupCommand() {};
            command.LPSResponseSetUpCommand.Execute(this);
            new Validator(this, command, _logger, _runtimeOperationIdProvider);
            if (command.IsValid && command.LPSResponseSetUpCommand.IsValid)
            {
                this.LocationToResponse = command.LocationToResponse;
                this.StatusCode = command.StatusCode;
                this.MIMEType= command.MIMEType;
                this.IsSuccessStatusCode= command.IsSuccessStatusCode;
                this.ResponseHeaders = new Dictionary<string, string>();
                this.ResponseContentHeaders = new Dictionary<string, string>();
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
