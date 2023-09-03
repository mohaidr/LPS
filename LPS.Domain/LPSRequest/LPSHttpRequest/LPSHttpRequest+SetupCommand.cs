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

    public partial class LPSHttpRequest
    {
        new public class SetupCommand : ICommand<LPSHttpRequest>, IValidCommand
        {
            public SetupCommand()
            {
                Httpversion = "1.1";
                HttpHeaders = new Dictionary<string, string>();
                ValidationErrors = new Dictionary<string, string>();
            }

            public string HttpMethod { get; set; }
            public string URL { get; set; }

            public string Payload { get; set; }

            public string Httpversion { get; set; }

            public Dictionary<string, string> HttpHeaders { get; set; }

            public bool IsValid { get; set; }
            public IDictionary<string, string> ValidationErrors { get; set; }

            public void Execute(LPSHttpRequest entity)
            {
                entity?.Setup(this);
            }
        }

        protected void Setup(SetupCommand command)
        {
            new Validator(this, command, _logger, _runtimeOperationIdProvider);

            if (command.IsValid)
            {
                this.HttpMethod = command.HttpMethod;


                this.Httpversion = command.Httpversion;


                this.URL = command.URL;


                this.Payload = command.Payload;


                this.HttpHeaders = new Dictionary<string, string>();

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

        internal void Clone(LPSHttpRequest cloneToEntity)
        {
            if (this.IsValid)
            {
                cloneToEntity.Id= this.Id;
                cloneToEntity.HttpMethod = this.HttpMethod;
                cloneToEntity.Httpversion = this.Httpversion;
                cloneToEntity.URL = this.URL;
                cloneToEntity.Payload = this.Payload;
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
        }



    }
}
