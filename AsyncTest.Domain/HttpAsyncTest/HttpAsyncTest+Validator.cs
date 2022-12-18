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

    public partial class HttpAsyncTest
    {
   
        public class Validator: IValidator<HttpAsyncTest, HttpAsyncTest.SetupCommand>
        {
            public Validator(HttpAsyncTest entity , SetupCommand dto)
            {
                Validate(entity, dto);
            }

            public void Validate(HttpAsyncTest entity,SetupCommand dto)
            {
                dto.IsValid = true;
                if (string.IsNullOrEmpty(dto.Name)  || !Regex.IsMatch(dto.Name, @"^[\w.-]{2,}$"))
                {
                    dto.IsValid = false;
                    Console.WriteLine("Invalid Test Name, The Name Should At least Be of 2 Charachters And Can Only Contains Letters, Numbers, ., _ and -");
                }

                if (dto.HttpRequestWrappers != null && dto.HttpRequestWrappers.Count>0)
                {
                    foreach (var command in dto.HttpRequestWrappers)
                    {
                        new HttpAsyncRequestWrapper.Validator(null, command);
                        if (!command.IsValid)
                        {
                            Console.WriteLine($"The http request named {command.Name} has an invalid input, please review the errors above");
                            dto.IsValid = false;
                        }
                    }
                }
                else
                {
                    dto.IsValid = false;
                    Console.WriteLine("Invalid async test, the test should at least contain one http request");
                }
            }
        }
    }
}

