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
using AsyncTest.Domain.Common;

namespace AsyncTest.Domain
{

    public partial class HttpAsyncRequest
    {
        public class Validator: IValidator<HttpAsyncRequest, SetupCommand>
        {
            public Validator(HttpAsyncRequest entity, SetupCommand dto)
            {
                Validate(entity,dto);
            }

            public void Validate(HttpAsyncRequest entity, SetupCommand dto)
            {
                dto.IsValid = true;
                //if you have to use the entity for validation purposes, check for nullability first
                Console.ForegroundColor = ConsoleColor.Yellow;

                if (dto == null)
                {
                    Console.WriteLine("Invalid Entity Command");
                }

                string[] httpMethods = { "GET", "HEAD", "POST", "PUT", "PATCH", "DELETE", "CONNECT", "OPTIONS", "TRACE" };

                if (dto.TimeOut <= 0)
                {
                    Console.WriteLine("The http  request timeout value should be a valid integer (less than 4 mins is recommended)");
                    dto.IsValid = false;
                }

                if (dto.HttpMethod == null || !httpMethods.Any(httpMethod => httpMethod == dto.HttpMethod.ToUpper()))
                {
                    Console.WriteLine("Invalid Http Method");
                    dto.IsValid = false;
                }

                if (dto.Httpversion != "1.0" && dto.Httpversion != "1.1")
                {
                    Console.WriteLine("Invalid Http Version, the value should be either 1.0 and 1.1");
                    dto.IsValid = false;
                }
                if (!(Uri.TryCreate(dto.URL, UriKind.Absolute, out Uri uriResult) && (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps || uriResult.Scheme.Contains("ws"))))
                {
                    Console.WriteLine("Invalid URL");
                    dto.IsValid = false;
                }

                Console.ResetColor();
                //TODO: Validate http headers

            }
        }
    }
}
