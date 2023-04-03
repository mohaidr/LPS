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

    public partial class LPSTestCase
    {
   
        public class Validator: IValidator<LPSTestCase, LPSTestCase.SetupCommand>
        {
            public Validator(LPSTestCase entity , SetupCommand dto)
            {
                Validate(entity, dto);
            }

            public void Validate(LPSTestCase entity,SetupCommand dto)
            {
                dto.IsValid = true;
                Console.ForegroundColor = ConsoleColor.Yellow;

                if (string.IsNullOrEmpty(dto.Name) || !Regex.IsMatch(dto.Name, @"^[\w.-]{2,}$"))
                {
                    Console.WriteLine("Please Provide a Valid Name, The Name Should At least Be of 2 Charachters And Can Only Contains Letters, Numbers, ., _ and -");
                    dto.IsValid = false;
                }


                if (!dto.Mode.HasValue)
                {
                    Console.WriteLine("Invalid combination, you have to use one of the below combinations");
                    Console.WriteLine("\t- Duration && Cool Down Time && Number Of Requests");
                    Console.WriteLine("\t- Duration && Cool Down Time && Batch Size");
                    Console.WriteLine("\t- Duration && Number Of Requests && Batch Size");
                    Console.WriteLine("\t- Cool Down Time && Number Of Requests && Batch Size");
                    Console.WriteLine("\t- Cool Down Time && Batch Size. Requests will not stop until you stop it");
                    Console.WriteLine("\t- Number Of Requests. Test will complete when all the requests are completed");
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

