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
        public class Validator: IValidator<LPSRequest, SetupCommand>
        {
            public Validator(LPSRequest entity, SetupCommand command)
            {
                Validate(entity,command);
            }

            public void Validate(LPSRequest entity, SetupCommand command)
            {
                command.IsValid = true;
                //if you have to use the entity for validation purposes, check for nullability first
                Console.ForegroundColor = ConsoleColor.Yellow;

                if (command == null)
                {
                    Console.WriteLine("Invalid Entity Command");
                }

                string[] httpMethods = { "GET", "HEAD", "POST", "PUT", "PATCH", "DELETE", "CONNECT", "OPTIONS", "TRACE" };

                if (command.TimeOut <= 0)
                {
                    Console.WriteLine("The http  request timeout value should be a valid integer (less than 4 mins is recommended)");
                    command.IsValid = false;
                }

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
