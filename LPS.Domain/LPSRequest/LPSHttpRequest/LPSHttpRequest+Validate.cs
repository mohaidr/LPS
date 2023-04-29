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
        public new class Validator: IValidator<LPSHttpRequest, LPSHttpRequest.SetupCommand>
        {
            public Validator(LPSHttpRequest entity, LPSHttpRequest.SetupCommand command) 
            {
                Validate(entity, command);
            }

            public void Validate(LPSHttpRequest entity, SetupCommand command)
            {
                command.IsValid = true;
                Console.ForegroundColor = ConsoleColor.Yellow;
                if (command == null)
                {
                    Console.WriteLine("Invalid Entity Command");
                }

                string[] httpMethods = { "GET", "HEAD", "POST", "PUT", "PATCH", "DELETE", "CONNECT", "OPTIONS", "TRACE" };

                if (command.HttpMethod == null || !httpMethods.Any(httpMethod => httpMethod == command.HttpMethod.ToUpper()))
                {
                    Console.WriteLine("Invalid Http Method");
                    command.IsValid = false;
                }

                if (command.Httpversion != "1.0" && command.Httpversion != "1.1")
                {
                    Console.WriteLine("Invalid Http Version, the value should be either 1.0 and 1.1");
                    command.IsValid = false;
                }
                if (!(Uri.TryCreate(command.URL, UriKind.Absolute, out Uri uriResult) && (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps || uriResult.Scheme.Contains("ws"))))
                {
                    Console.WriteLine("Invalid URL");
                    command.IsValid = false;
                }

                Console.ResetColor();
                //TODO: Validate http headers
            }
        }
    }
}
