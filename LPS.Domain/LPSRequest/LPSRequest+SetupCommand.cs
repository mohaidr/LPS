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

    public partial class LPSRequest
    {
        public class SetupCommand : ICommand<LPSRequest>
        {
            public SetupCommand()
            {
                TimeOut = 4;
                Httpversion = "1.1";
                HttpHeaders = new Dictionary<string, string>();
            }

            public string HttpMethod { get; set; }
            public string URL { get; set; }

            public string Payload { get; set; }

            public string Httpversion { get; set; }

            public Dictionary<string, string> HttpHeaders { get; set; }

            public int TimeOut { get; set; }

            public bool IsValid { get; set; }

            public void Execute(LPSRequest entity)
            {
                entity?.Setup(this);
            }
        }


        private void Setup(SetupCommand dto)
        {
            new Validator(this, dto);

            if (dto.IsValid)
            {
                this.HttpRequestTimeout = dto.TimeOut;

                this.HttpMethod = dto.HttpMethod;


                this.Httpversion = dto.Httpversion;


                this.URL = dto.URL;


                this.Payload = dto.Payload;


                this.HttpHeaders = new Dictionary<string, string>();

                if (dto.HttpHeaders != null)
                {
                    foreach (var header in dto.HttpHeaders)
                    {
                        this.HttpHeaders.Add(header.Key, header.Value);
                    }
                }

                this.IsValid = true;
            }
        }

    }
}
