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

    public partial class LPSTest
    {
   
        public class Validator: IValidator<LPSTest, LPSTest.SetupCommand>
        {
            public Validator(LPSTest entity , SetupCommand dto)
            {
                Validate(entity, dto);
            }

            public void Validate(LPSTest entity,SetupCommand dto)
            {
                dto.IsValid = true;
                if (string.IsNullOrEmpty(dto.Name)  || !Regex.IsMatch(dto.Name, @"^[\w.-]{2,}$"))
                {
                    dto.IsValid = false;
                    Console.WriteLine("Invalid Test Name, The Name Should At least Be of 2 Charachters And Can Only Contains Letters, Numbers, ., _ and -");
                }

                if (dto.lpsRequestWrappers != null && dto.lpsRequestWrappers.Count>0)
                {
                    foreach (var command in dto.lpsRequestWrappers)
                    {
                        new LPSRequestWrapper.Validator(null, command);
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

