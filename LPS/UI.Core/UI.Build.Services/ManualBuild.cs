using LPS.UI.Common;
using LPS.Domain;
using LPS.Domain.Common;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace LPS.UI.Core.UI.Build.Services
{
    internal class ManualBuild : IBuilderService<LPSTest.SetupCommand, LPSTest>
    {
        LPSRequestWrapperValidator _validator;
        public ManualBuild(LPSRequestWrapperValidator validator)
        {
            _validator= validator;
        }


        static bool skipOptionalFields = true;

        public void Build(LPSTest.SetupCommand lpsTestCommand)
        {
            Console.WriteLine("To skip optional fields enter (Y), otherwise enter (N)");
            string decision = Console.ReadLine();

            if (decision.Trim().ToLower() == "n")
                skipOptionalFields = false;
            else
                skipOptionalFields = true;

            lpsTestCommand.lpsRequestWrappers = new List<LPSRequestWrapper.SetupCommand>();

            Console.WriteLine("Start building your collection of requests");

            string[] httpMethods = { "GET", "HEAD", "POST", "PUT", "PATCH", "DELETE", "CONNECT", "OPTIONS", "TRACE" };
            LPSRequestWrapper.SetupCommand lpsRequestWrapperCommand = new LPSRequestWrapper.SetupCommand();

            while (true)
            {
                Console.WriteLine("Test name should at least be of 2 charachters and can only contains letters, numbers, ., _ and -");
                if (!skipOptionalFields && (string.IsNullOrEmpty(lpsTestCommand.Name) || !Regex.IsMatch(lpsTestCommand.Name, @"^[\w.-]{2,}$")))
                {
                    lpsTestCommand.Name = ChallengeService.Challenge("-testName");
                    continue;
                } 
                else if (skipOptionalFields)
                {
                    lpsTestCommand.Name = DateTime.Now.ToFileTime().ToString();
                }

                if (!skipOptionalFields && !_validator.Validate("-requestname", lpsRequestWrapperCommand))
                {
                    Console.WriteLine("Give your request a name, it should at least be 2 charachters and can only contains letters, numbers, ., _ and -");
                    lpsRequestWrapperCommand.Name =  ChallengeService.Challenge("-requestname");
                    continue;
                }

                if (!_validator.Validate("-httpmethod", lpsRequestWrapperCommand))
                {
                    Console.WriteLine("Enter a Valid Http Method");
                    lpsRequestWrapperCommand.LPSRequest.HttpMethod= ChallengeService.Challenge("-httpmethod");
                    continue;
                }

                if (!_validator.Validate("-url", lpsRequestWrapperCommand))
                {
                    Console.WriteLine("Enter a valid URL e.g (http(s)://example.com)");
                    lpsRequestWrapperCommand.LPSRequest.URL = ChallengeService.Challenge("-url");
                    continue;
                }

                if (!_validator.Validate("-repeat", lpsRequestWrapperCommand))
                {
                    try
                    {
                        Console.WriteLine("Enter the number of async calls, it should be a valid positive integer number");
                        lpsRequestWrapperCommand.NumberofAsyncRepeats = int.Parse(ChallengeService.Challenge("-repeat"));
                        continue;
                    }
                    catch {
                        continue;
                    }
                }

                switch (skipOptionalFields)
                {
                    case false:
                        {
                            if (!_validator.Validate("-timeout", lpsRequestWrapperCommand))
                            {
                                try
                                {
                                    Console.WriteLine("Enter the HTTP request timeout value in minutes, it should be a valid positive integer number");
                                    lpsRequestWrapperCommand.LPSRequest.TimeOut = int.Parse(ChallengeService.Challenge("-timeout"));
                                    continue;
                                }
                                catch { }
                            }
                            if (!_validator.Validate("-httpversion", lpsRequestWrapperCommand))
                            {
                                Console.WriteLine("Enter a valid http version, currently we only supports 1.0 and 1.1");
                                lpsRequestWrapperCommand.LPSRequest.Httpversion = ChallengeService.Challenge("-httpversion");
                                continue;
                            }
                        }
                        break;
                    case true:
                        ChallengeService.SetOptionalFeildsToDefaultValues(lpsRequestWrapperCommand);
                        break;
                }

                Console.WriteLine("Enter the request headers");
                Console.WriteLine("Type your header in the following format (headerName: headerValue) and click enter. After you finish entering all headers, enter done");
                lpsRequestWrapperCommand.LPSRequest.HttpHeaders = InputHeaderService.Challenge();


                if (lpsRequestWrapperCommand.LPSRequest.HttpMethod.ToUpper() == "PUT" || lpsRequestWrapperCommand.LPSRequest.HttpMethod.ToUpper() == "POST" || lpsRequestWrapperCommand.LPSRequest.HttpMethod.ToUpper() == "PATCH")
                {
                    Console.WriteLine("Add payload to the request, enter a valid path to read the payload from");
                    lpsRequestWrapperCommand.LPSRequest.Payload = InputPayloadService.Challenge();
                }

                lpsTestCommand.lpsRequestWrappers.Add(lpsRequestWrapperCommand);

                Console.WriteLine("Enter \"add\" to add new http request to the collection or click enter to start your test");

                string action = Console.ReadLine().Trim().ToLower();
                if (action == "add")
                {
                    lpsRequestWrapperCommand = new LPSRequestWrapper.SetupCommand();
                    continue;
                }
                break;
            }
        }
    }
}
