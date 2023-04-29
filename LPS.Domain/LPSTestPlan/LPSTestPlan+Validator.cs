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

    public partial class LPSTestPlan
    {
   
        public class Validator: IValidator<LPSTestPlan, LPSTestPlan.SetupCommand>
        {
            public Validator(LPSTestPlan entity , SetupCommand command)
            {
                Validate(entity, command);
            }

            public void Validate(LPSTestPlan entity,SetupCommand command)
            {
                command.IsValid = true;
                if (string.IsNullOrEmpty(command.Name)  || !Regex.IsMatch(command.Name, @"^[\w.-]{2,}$"))
                {
                    command.IsValid = false;
                    Console.WriteLine("Invalid Test Name, The Name Should At least Be of 2 Charachters And Can Only Contains Letters, Numbers, ., _ and -");
                }
                if (command.NumberOfClients < 1)
                {
                    command.IsValid = false;
                    Console.WriteLine("Number of clients can't be less than 1, at least one user has to be created.");
                }
                if (command.MaxConnectionsPerServer < 1)
                {
                    command.IsValid = false;
                    Console.WriteLine("Max connections per server can't be less than 1.");
                }

                if (command.ClientTimeout < 1)
                {
                    command.IsValid = false;
                    Console.WriteLine("Client Timeout can't be less than 1.");
                }

                if (command.PooledConnectionIdleTimeout < 1)
                {
                    command.IsValid = false;
                    Console.WriteLine("Pooled connection idle timeout can't be less than 1 minute.");
                }
                if (command.PooledConnectionLifetime < 1)
                {
                    command.IsValid = false;
                    Console.WriteLine("Pooled connection life time can't be less than 1 minute.");
                }

                if (!command.DelayClientCreationUntilIsNeeded.HasValue) 
                { 
                    command.IsValid = false;
                    Console.WriteLine("Delay client creation until needed can't be empty.");
                }

                if (command.LPSTestCases != null && command.LPSTestCases.Count>0)
                {
                    foreach (var lpsTestCaseCommand in command.LPSTestCases)
                    {
                        new LPSHttpTestCase.Validator(null, lpsTestCaseCommand);
                        if (!lpsTestCaseCommand.IsValid)
                        {
                            Console.WriteLine($"The http request named {lpsTestCaseCommand.Name} has an invalid input, please review the above errors and fix them");
                            command.IsValid = false;
                        }
                    }
                }
                else
                {
                    command.IsValid = false;
                    Console.WriteLine("Invalid async test, the test should at least contain one http request");
                }
            }
        }
    }
}

