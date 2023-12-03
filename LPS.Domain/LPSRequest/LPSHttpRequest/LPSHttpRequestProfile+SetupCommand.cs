using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using LPS.Domain.Common;

namespace LPS.Domain
{

    public partial class LPSHttpRequestProfile
    {
        new public class SetupCommand : ICommand<LPSHttpRequestProfile>, IValidCommand<LPSHttpRequestProfile>
        {
            public SetupCommand()
            {
                Httpversion = "2.0";
                DownloadHtmlEmbeddedResources= false;
                SaveResponse= false;
                HttpHeaders = new Dictionary<string, string>();
                ValidationErrors = new Dictionary<string, List<string>>();
            }

            public string HttpMethod { get; set; }
            public string URL { get; set; }

            public string Payload { get; set; }

            public string Httpversion { get; set; }

            public Dictionary<string, string> HttpHeaders { get; set; }

            public bool IsValid { get; set; }
            public IDictionary<string, List<string>> ValidationErrors { get; set; }
            public bool? DownloadHtmlEmbeddedResources { get; set; }
            public bool? SaveResponse { get; set; }
            public void Execute(LPSHttpRequestProfile entity)
            {
                entity?.Setup(this);
            }

            internal LPSRequestProfile.SetupCommand LPSRequestProfileSetUpCommand;

        }

        protected void Setup(SetupCommand command)
        {
            //Set the inherited properties through the parent entity setup command
            command.LPSRequestProfileSetUpCommand = new LPSRequestProfile.SetupCommand() {};
            command.LPSRequestProfileSetUpCommand.Execute(this);
            new Validator(this, command, _logger, _runtimeOperationIdProvider);
            if (command.IsValid && command.LPSRequestProfileSetUpCommand.IsValid)
            {
                this.HttpMethod = command.HttpMethod;
                this.Httpversion = command.Httpversion;
                this.URL = command.URL;
                this.Payload = command.Payload;
                this.HttpHeaders = new Dictionary<string, string>();
                this.DownloadHtmlEmbeddedResources = command.DownloadHtmlEmbeddedResources.Value;
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
        }

        public object Clone()
        {
            LPSHttpRequestProfile cloneToEntity = new LPSHttpRequestProfile(_logger, _watchdog, _runtimeOperationIdProvider);
            if (this.IsValid)
            {
                cloneToEntity.Id= this.Id;
                cloneToEntity.HttpMethod = this.HttpMethod;
                cloneToEntity.Httpversion = this.Httpversion;
                cloneToEntity.URL = this.URL;
                cloneToEntity.Payload = this.Payload;
                cloneToEntity.DownloadHtmlEmbeddedResources = this.DownloadHtmlEmbeddedResources;
                cloneToEntity.SaveResponse = this.SaveResponse;
                cloneToEntity.HttpHeaders = new Dictionary<string, string>();
                if (this.HttpHeaders != null)
                {
                    foreach (var header in this.HttpHeaders)
                    {
                        cloneToEntity.HttpHeaders.Add(header.Key, header.Value);
                    }
                }
                cloneToEntity.IsValid = true;
            }
            return cloneToEntity;
        }
    }
}
