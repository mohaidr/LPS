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

    public partial class HttpAsyncRequestWrapper
    {
   
        public class Validator: IValidator<HttpAsyncRequestWrapper, HttpAsyncRequestWrapper.SetupCommand>
        {
            public Validator(HttpAsyncRequestWrapper entity , SetupCommand dto)
            {
                Validate(entity, dto);
            }

            public void Validate(HttpAsyncRequestWrapper entity,SetupCommand dto)
            {
                dto.IsValid = true;
                Console.ForegroundColor = ConsoleColor.Yellow;

                if (string.IsNullOrEmpty(dto.Name) || !Regex.IsMatch(dto.Name, @"^[\w.-]{2,}$"))
                {
                    Console.WriteLine("Please Provide a Valid Name, The Name Should At least Be of 2 Charachters And Can Only Contains Letters, Numbers, ., _ and -");
                    dto.IsValid = false;
                }

                if (dto.NumberofAsyncRepeats <= 0)
                {
                    Console.WriteLine("The number of Async requests should be a valid integer");
                    dto.IsValid = false;
                }

                if (dto.HttpRequest != null)
                {
                        new HttpAsyncRequest.Validator(null, dto.HttpRequest);
                        if (!dto.HttpRequest.IsValid)
                        {
                            Console.WriteLine("Invalid Http Request");
                            dto.IsValid = false;
                        }
                }
                Console.ResetColor();

            }
        }
    }
}

