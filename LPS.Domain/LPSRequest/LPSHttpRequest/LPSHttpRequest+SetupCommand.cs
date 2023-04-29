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
        new public class SetupCommand : ICommand<LPSHttpRequest>
        {
            public SetupCommand()
            {
                Httpversion = "1.1";
                HttpHeaders = new Dictionary<string, string>();
            }

            public string HttpMethod { get; set; }
            public string URL { get; set; }

            public string Payload { get; set; }

            public string Httpversion { get; set; }

            public Dictionary<string, string> HttpHeaders { get; set; }

            public bool IsValid { get; set; }

            public void Execute(LPSHttpRequest entity)
            {
                entity?.Setup(this);
            }
        }

        protected void Setup(SetupCommand command)
        {
            new Validator(this, command);

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

    }
}
