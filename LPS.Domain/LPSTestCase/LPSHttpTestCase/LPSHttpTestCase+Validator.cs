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

    public partial class LPSHttpTestCase
    {
   
        public class Validator: IValidator<LPSHttpTestCase, LPSHttpTestCase.SetupCommand>
        {
            public Validator(LPSHttpTestCase entity , SetupCommand command)
            {
                Validate(entity, command);
            }

            public void Validate(LPSHttpTestCase entity,SetupCommand command)
            {
                command.IsValid = true;
                Console.ForegroundColor = ConsoleColor.Yellow;

                if (string.IsNullOrEmpty(command.Name) || !Regex.IsMatch(command.Name, @"^[\w.-]{2,}$"))
                {
                    Console.WriteLine("Please Provide a Valid Name, The Name Should At least Be of 2 Charachters And Can Only Contains Letters, Numbers, ., _ and -");
                    command.IsValid = false;
                }


                if (!command.Mode.HasValue)
                {
                    Console.WriteLine("Invalid combination, you have to use one of the below combinations");
                    Console.WriteLine("\t- Duration && Cool Down Time && Batch Size");
                    Console.WriteLine("\t- Cool Down Time && Number Of Requests && Batch Size");
                    Console.WriteLine("\t- Cool Down Time && Batch Size. Requests will not stop until you stop it");
                    Console.WriteLine("\t- Number Of Requests. Test will complete when all the requests are completed");
                    Console.WriteLine("\t- Duration. Test will complete once the duration expires");
                    command.IsValid = false;
                }

                if (command.Duration.HasValue && command.CoolDownTime.HasValue && command.CoolDownTime.Value > command.Duration.Value)
                {
                    Console.WriteLine("Cool Down Time can't be larger than the Duration");
                }

                if (command.RequestCount.HasValue && command.BatchSize.HasValue && command.BatchSize.Value > command.RequestCount.Value)
                {
                    Console.WriteLine("Batch Size can't be larger than the request count");
                }


                if (command.LPSRequest != null)
                {
                        new LPSHttpRequest.Validator(null, command.LPSRequest);
                        if (!command.LPSRequest.IsValid)
                        {
                            Console.WriteLine("Invalid Http Request");
                            command.IsValid = false;
                        }
                }
                Console.ResetColor();

            }
        }
    }
}

