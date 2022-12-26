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

    public partial class LPSRequestWrapper
    {
   
        public class Validator: IValidator<LPSRequestWrapper, LPSRequestWrapper.SetupCommand>
        {
            public Validator(LPSRequestWrapper entity , SetupCommand dto)
            {
                Validate(entity, dto);
            }

            public void Validate(LPSRequestWrapper entity,SetupCommand dto)
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

                if (dto.LPSRequest != null)
                {
                        new LPSRequest.Validator(null, dto.LPSRequest);
                        if (!dto.LPSRequest.IsValid)
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

