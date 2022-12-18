using AsyncCalls.UI.Common;
using AsyncTest.Domain;
using AsyncTest.Domain.Common;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace AsyncCalls.UI.Core.UI.Build.Services
{
    internal class ManualBuild : IBuilderService<HttpAsyncTest.SetupCommand, HttpAsyncTest>
    {
        private readonly IFileLogger _logger;
        private readonly IConfiguration _config;
        string[]  _args;
        HttpAsyncRequestWrapperValidator _validator;
        public ManualBuild(IFileLogger loggger, IConfiguration config, HttpAsyncRequestWrapperValidator validator, dynamic cmdArgs)
        {
            _logger = loggger;
            _config = config;
            _validator= validator;
            _args = cmdArgs.args;

        }


        static bool skipOptionalFields = true;

        public void Build(HttpAsyncTest.SetupCommand httpAsyncTestCommand)
        {
            Console.WriteLine("To skip optional fields enter (Y), otherwise enter (N)");
            string decision = Console.ReadLine();

            if (decision.Trim().ToLower() == "n")
                skipOptionalFields = false;
            else
                skipOptionalFields = true;

            httpAsyncTestCommand.HttpRequestWrappers = new List<HttpAsyncRequestWrapper.SetupCommand>();

            Console.WriteLine("Start building your collection of requests");

            string[] httpMethods = { "GET", "HEAD", "POST", "PUT", "PATCH", "DELETE", "CONNECT", "OPTIONS", "TRACE" };
            HttpAsyncRequestWrapper.SetupCommand httpRequestWrapperCommand = new HttpAsyncRequestWrapper.SetupCommand();

            while (true)
            {
                Console.WriteLine("Test name should at least be of 2 charachters and can only contains letters, numbers, ., _ and -");
                if (!skipOptionalFields && string.IsNullOrEmpty(httpAsyncTestCommand.Name) || !Regex.IsMatch(httpAsyncTestCommand.Name, @"^[\w.-]{2,}$"))
                {
                    httpAsyncTestCommand.Name = ChallengeService.Challenge("-testName");
                    continue;
                } 
                else if (skipOptionalFields)
                {
                    httpAsyncTestCommand.Name = DateTime.Now.ToFileTime().ToString();
                }

                if (!skipOptionalFields && !_validator.Validate("-requestname", httpRequestWrapperCommand))
                {
                    Console.WriteLine("Give your request a name, it should at least be 2 charachters and can only contains letters, numbers, ., _ and -");
                    httpRequestWrapperCommand.Name =  ChallengeService.Challenge("-requestname");
                    continue;
                }

                if (!_validator.Validate("-httpmethod", httpRequestWrapperCommand))
                {
                    Console.WriteLine("Enter a Valid Http Method");
                    httpRequestWrapperCommand.HttpRequest.HttpMethod= ChallengeService.Challenge("-httpmethod");
                    continue;
                }

                if (!_validator.Validate("-url", httpRequestWrapperCommand))
                {
                    Console.WriteLine("Enter a valid URL e.g (http(s)://example.com)");
                    httpRequestWrapperCommand.HttpRequest.URL = ChallengeService.Challenge("-url");
                    continue;
                }

                if (!_validator.Validate("-repeat", httpRequestWrapperCommand))
                {
                    try
                    {
                        Console.WriteLine("Enter the number of async calls, it should be a valid positive integer number");
                        httpRequestWrapperCommand.NumberofAsyncRepeats = int.Parse(ChallengeService.Challenge("-repeat"));
                        continue;
                    }
                    catch { }
                }

                switch (skipOptionalFields)
                {
                    case false:
                        {
                            if (!_validator.Validate("-timeout", httpRequestWrapperCommand))
                            {
                                try
                                {
                                    Console.WriteLine("Enter the HTTP request timeout value in minutes, it should be a valid positive integer number");
                                    httpRequestWrapperCommand.HttpRequest.TimeOut = int.Parse(ChallengeService.Challenge("-timeout"));
                                    continue;
                                }
                                catch { }
                            }
                            if (!_validator.Validate("-httpversion", httpRequestWrapperCommand))
                            {
                                Console.WriteLine("Enter a valid http version, currently we only supports 1.0 and 1.1");
                                httpRequestWrapperCommand.HttpRequest.Httpversion = ChallengeService.Challenge("-httpversion");
                                continue;
                            }
                        }
                        break;
                    case true:
                        ChallengeService.SetOptionalFeildsToDefaultValues(httpRequestWrapperCommand);
                        break;
                }

                Console.WriteLine("Enter the request headers");
                Console.WriteLine("Type your header in the following format (headerName: headerValue) and click enter. After you finish entering all headers, enter done");
                httpRequestWrapperCommand.HttpRequest.HttpHeaders = InputHeaderService.Challenge();


                if (httpRequestWrapperCommand.HttpRequest.HttpMethod.ToUpper() == "PUT" || httpRequestWrapperCommand.HttpRequest.HttpMethod.ToUpper() == "POST" || httpRequestWrapperCommand.HttpRequest.HttpMethod.ToUpper() == "PATCH")
                {
                    Console.WriteLine("Add payload to the request, enter a valid path to read the payload from");
                    httpRequestWrapperCommand.HttpRequest.Payload = InputPayloadService.Challenge();
                }

                httpAsyncTestCommand.HttpRequestWrappers.Add(httpRequestWrapperCommand);

                Console.WriteLine("Enter \"add\" to add new http request to the collection or click enter to start your test");

                string action = Console.ReadLine().Trim().ToLower();
                if (action == "add")
                {
                    httpRequestWrapperCommand = new HttpAsyncRequestWrapper.SetupCommand();
                    continue;
                }
                break;
            }
        }
    }
}
