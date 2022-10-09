using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using AsyncTest.Domain;
using AsyncTest.Domain.Common;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Serilog;

namespace AsyncTest.UI.Core
{

    //redesign this class to remove redundent code and unify both manual and command build functions if it makes sense, also remove unnecessary complexity 
    internal class TestService<T1, T2> : ITestService<T1, T2> where T1: ICommand<T2> where T2: IExecutable
    {
        private readonly ILogger _logger;
        private readonly IConfiguration _config;
        public TestService(ILogger<T1> loggger, IConfiguration config)
        {
            _logger = loggger;
          
            _config = config;
        }

        static bool skipOptionalFields = true;

        static Dictionary<string, bool> requiredProperties = new Dictionary<string, bool>()
            {
                { "-Add", false }, { "-a", false },
                { "-url",false },
                { "-httpmethod",false },{ "-hm",false },
                { "-header",false },{ "-h",false },
                { "-repeat",false }, { "-r",false }
            };

        static Dictionary<string, bool> optionalProperties = new Dictionary<string, bool>()
            {
                { "-name", false },{ "-n", false },
                { "-payload", false },{ "-p", false },
                { "-requestname", false },{ "-rn", false },
                { "-httpversion", false }, { "-hv", false },
                { "-timeout", false },{ "-t", false },
            };


        static string[] requiredPropsAsArray = requiredProperties.Keys.ToArray();

        public static void BuildFromCommand(HttpAsyncTest.SetupCommand httpAsyncTestDto, string[] args)
        {

            int numberofRequests = args.Count(arg => (arg == "-add" || arg == "-a"));

            if (numberofRequests == 0)
            {
                throw new ArgumentException("-Add property is required");
            }

            if (args.Count(arg => arg != "-add") % 2 != 0)
                throw new ArgumentException("A valid property should start with - and followed by its value");

            args = Array.ConvertAll(args, arg => arg.ToLower());

            httpAsyncTestDto.Name = ((args[0] == "-name" || args[0] == "-n") && !string.IsNullOrEmpty(args[0])) ? args[0] : DateTime.Now.ToFileTime().ToString();
            HttpAsyncRequestContainer.SetupCommand httpRequestContainerDto = new HttpAsyncRequestContainer.SetupCommand();

            string[] kvp = new string[2];
            httpAsyncTestDto.IsCommandLine = true;

            for (int i = 0; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "-add":
                    case "-a":
                        if (i > 2)
                        {
                            //Fill Empty Required Arguments
                            ForceRequiredCommandArguments(httpRequestContainerDto);
                        }
                        ResetProperties();
                        requiredProperties["-add"] = true;
                        requiredProperties["-a"] = true;
                        httpRequestContainerDto = new HttpAsyncRequestContainer.SetupCommand();
                        SetOptionalArgsToDefaultValues(httpRequestContainerDto);
                        httpAsyncTestDto.HttpRequestContainers.Add(httpRequestContainerDto);
                        break;
                    case "-requestname":
                    case "-rn":
                        httpRequestContainerDto.Name = args[++i];
                        if (!ValidateUserInput("-requestname", httpRequestContainerDto))
                        {
                            SetByProperty("-requestname", httpRequestContainerDto);
                            args[i] = httpRequestContainerDto.Name;
                            i -= 2;
                            continue;
                        }
                        optionalProperties["-requestname"] = true;
                        optionalProperties["-rn"] = true;
                        break;
                    case "-httpversion":
                    case "-hv":
                        httpRequestContainerDto.HttpRequest.Httpversion = args[++i];
                        if (!ValidateUserInput("-httpversion", httpRequestContainerDto))
                        {
                            SetByProperty("-httpversion", httpRequestContainerDto);
                            args[i] = httpRequestContainerDto.HttpRequest.Httpversion;
                            i -= 2;
                            continue;
                        }
                        optionalProperties["-httpversion"] = true;
                        optionalProperties["-hv"] = true;
                        break;
                    case "-httpmethod":
                    case "-hm":
                        httpRequestContainerDto.HttpRequest.HttpMethod = args[++i];
                        if (!ValidateUserInput("-httpmethod", httpRequestContainerDto))
                        {
                            SetByProperty("-httpmethod", httpRequestContainerDto);
                            args[i] = httpRequestContainerDto.HttpRequest.HttpMethod;
                            i -= 2;
                            continue;
                        }
                        requiredProperties["-httpmethod"] = true;
                        requiredProperties["-hm"] = true;
                        break;
                    case "-timeout":
                    case "-t":
                        try
                        {
                            httpRequestContainerDto.HttpRequest.HttpRequestTimeOut = int.Parse(args[++i]);
                        }
                        catch
                        {
                            httpRequestContainerDto.HttpRequest.HttpRequestTimeOut = -1;
                            args[i] = httpRequestContainerDto.HttpRequest.HttpRequestTimeOut.ToString();
                        }
                        if (!ValidateUserInput("-timeout", httpRequestContainerDto))
                        {
                            SetByProperty("-timeout", httpRequestContainerDto);
                            args[i] = httpRequestContainerDto.HttpRequest.HttpRequestTimeOut.ToString();
                            i -= 2;
                            continue;
                        }
                        optionalProperties["-timeout"] = true;
                        optionalProperties["-t"] = true;
                        break;
                    case "-repeat":
                    case "-r":
                        try
                        {
                            httpRequestContainerDto.NumberofAsyncRepeats = int.Parse(args[++i]);
                        }
                        catch
                        {
                            httpRequestContainerDto.NumberofAsyncRepeats = -1;
                            args[i] = httpRequestContainerDto.NumberofAsyncRepeats.ToString();
                        }
                        if (!ValidateUserInput("-repeat", httpRequestContainerDto))
                        {
                            SetByProperty("-repeat", httpRequestContainerDto);
                            args[i] = httpRequestContainerDto.NumberofAsyncRepeats.ToString();
                            i -= 2;
                            continue;
                        }
                        requiredProperties["-repeat"] = true;
                        requiredProperties["-r"] = true;
                        break;
                    case "-header":
                    case "-h":
                        kvp = args[++i].Split(':');
                        if (kvp.Length == 2)
                            httpRequestContainerDto.HttpRequest.HttpHeaders.Add(kvp[0], kvp[1]);
                        else
                            throw new ArgumentException("Invalid Header Value");
                        requiredProperties["-header"] = true;
                        requiredProperties["-h"] = true;
                        break;
                    case "-url":
                        httpRequestContainerDto.HttpRequest.URL = args[++i];
                        if (!ValidateUserInput("-url", httpRequestContainerDto))
                        {
                            SetByProperty("-url", httpRequestContainerDto);
                            args[i] = httpRequestContainerDto.HttpRequest.URL;
                            i -= 2;
                            continue;
                        }
                        requiredProperties["-url"] = true;
                        break;
                    case "-payload":
                    case "-p":
                        bool loop = true;
                        i++;
                        while (!SetPayload(httpRequestContainerDto.HttpRequest, args[i]) && loop)
                        {
                            Console.WriteLine("Would you like to retry to read the payload? (Y) to retry, (N) to cancel the test, (P) to Enter a New Path, (C) to continue without payload");
                            string retryDecision = Console.ReadLine();
                            switch (retryDecision.Trim().ToLower())
                            {
                                case "y":
                                    break;
                                case "p":
                                    args[i] = Console.ReadLine();
                                    break;
                                case "c":
                                    loop = false;
                                    break;
                                case "n":
                                    throw new Exception("Test Cancelled");
                            }
                        }
                        optionalProperties["-payload"] = true;
                        optionalProperties["-p"] = true;
                        break;
                    default:
                        throw new ArgumentException($"{args[i]} is an invalid argument");
                }
            }
            ForceRequiredCommandArguments(httpRequestContainerDto);
        }

        public static void ForceRequiredCommandArguments(HttpAsyncRequestContainer.SetupCommand httpRequestContainerDto)
        {

            for (int j = 0; j < requiredPropsAsArray.Length; j++)
            {
                if (requiredProperties[requiredPropsAsArray[j]] == false)
                {
                    SetByProperty(requiredPropsAsArray[j], httpRequestContainerDto);
                    if (!ValidateUserInput(requiredPropsAsArray[j], httpRequestContainerDto))
                    {
                        j--;
                    }
                }
            }
        }

        public static void BuildManual(HttpAsyncTest.SetupCommand httpAsyncTestDto)
        {
            Console.WriteLine("To skip optional fields enter (Y), otherwise enter (N)");
            string decision = Console.ReadLine();

            if (decision.Trim().ToLower() == "n")
                skipOptionalFields = false;
            else
                skipOptionalFields = true;

            httpAsyncTestDto.HttpRequestContainers = new List<HttpAsyncRequestContainer.SetupCommand>();


            if (skipOptionalFields)
            {
                httpAsyncTestDto.Name = DateTime.Now.ToFileTime().ToString();
            }
            else
            {
                while (true)
                {
                    Console.WriteLine("Test name should at least be of 2 charachters and can only contains letters, numbers, ., _ and -");
                    httpAsyncTestDto.Name = Console.ReadLine().Trim();
                    if (string.IsNullOrEmpty(httpAsyncTestDto.Name) || !Regex.IsMatch(httpAsyncTestDto.Name, @"^[\w.-]{2,}$"))
                    {
                        continue;
                    }

                    break;
                }
            }


            Console.WriteLine("Start building your collection of requests");

            string[] httpMethods = { "GET", "HEAD", "POST", "PUT", "PATCH", "DELETE", "CONNECT", "OPTIONS", "TRACE" };
            HttpAsyncRequestContainer.SetupCommand httpRequestContainerDto = new HttpAsyncRequestContainer.SetupCommand();

            while (true)
            {

                if (!skipOptionalFields && !ValidateUserInput("-requestname", httpRequestContainerDto))
                {
                    Console.WriteLine("Give your request a name, it should at least be 2 charachters and can only contains letters, numbers, ., _ and -");
                    SetByProperty("-requestname", httpRequestContainerDto);
                    continue;
                }

                if (!ValidateUserInput("-httpmethod", httpRequestContainerDto))
                {
                    Console.WriteLine("Enter a Valid Http Method");
                    SetByProperty("-httpmethod", httpRequestContainerDto);
                    continue;
                }

                if (!ValidateUserInput("-url", httpRequestContainerDto))
                {
                    Console.WriteLine("Enter a valid URL e.g (http(s)://example.com)");
                    SetByProperty("-url", httpRequestContainerDto);
                    continue;
                }

                if (!ValidateUserInput("-repeat", httpRequestContainerDto))
                {
                    Console.WriteLine("Enter the number of async calls, it should be a valid positive integer number");
                    SetByProperty("-repeat", httpRequestContainerDto);
                    continue;
                }

                switch (skipOptionalFields)
                {
                    case false:
                        {
                            if (!ValidateUserInput("-timeout", httpRequestContainerDto))
                            {
                                Console.WriteLine("Enter the HTTP request timeout value in minutes, it should be a valid positive integer number");
                                SetByProperty("-timeout", httpRequestContainerDto);
                                continue;
                            }
                            if (!ValidateUserInput("-httpversion", httpRequestContainerDto))
                            {
                                Console.WriteLine("Enter a valid http version, currently we only supports 1.0 and 1.1");
                                SetByProperty("-httpversion", httpRequestContainerDto);
                                continue;
                            }
                            if (httpRequestContainerDto.HttpRequest.HttpMethod.ToUpper() == "PUT" || httpRequestContainerDto.HttpRequest.HttpMethod.ToUpper() == "POST" || httpRequestContainerDto.HttpRequest.HttpMethod.ToUpper() == "PATCH")
                            {
                                //Read Payload
                                Console.WriteLine("Add payload to the request, enter valid path to read the payload from");
                                bool loop = true;
                                string path = Console.ReadLine().Trim();
                                while (!SetPayload(httpRequestContainerDto.HttpRequest, path) && loop)
                                {
                                    Console.WriteLine("Would you like to retry to read the payload? (Y) to retry, (N) to cancel the test, (C) to continue without payload");
                                    string retryDecision = Console.ReadLine();
                                    switch (retryDecision.Trim().ToLower())
                                    {
                                        case "y":
                                            break;
                                        case "n":
                                            throw new Exception("Test Cancelled");
                                        case "c":
                                            loop = false;
                                            break;
                                    }
                                }
                            }
                        }
                        break;
                    case true:
                        SetOptionalArgsToDefaultValues(httpRequestContainerDto);
                        break;
                }

                SetHeaders(httpRequestContainerDto.HttpRequest);


                httpAsyncTestDto.HttpRequestContainers.Add(httpRequestContainerDto);

                Console.WriteLine("Enter \"add\" to add new http request to the collection or click enter to start your test");

                string action = Console.ReadLine().Trim().ToLower();
                if (action == "add")
                {
                    httpRequestContainerDto = new HttpAsyncRequestContainer.SetupCommand();
                    continue;
                }
                break;
            }
        }

        static void SetOptionalArgsToDefaultValues(HttpAsyncRequestContainer.SetupCommand httpRequestContainerDto)
        {
            httpRequestContainerDto.Name = !string.IsNullOrEmpty(httpRequestContainerDto.Name) ? httpRequestContainerDto.Name : DateTime.Now.Ticks.ToString();
            httpRequestContainerDto.HttpRequest.HttpRequestTimeOut = httpRequestContainerDto.HttpRequest.HttpRequestTimeOut == 0 ? 4 : httpRequestContainerDto.HttpRequest.HttpRequestTimeOut;
            httpRequestContainerDto.HttpRequest.Httpversion = httpRequestContainerDto.HttpRequest.Httpversion ?? "1.1";
            httpRequestContainerDto.HttpRequest.Payload = httpRequestContainerDto.HttpRequest.Payload ?? string.Empty;
        }

        static void SetByProperty(string property, HttpAsyncRequestContainer.SetupCommand httpRequestContainerDto)
        {
            switch (property)
            {
                case "-requestname":
                    Console.Write("Request Name: ");
                    httpRequestContainerDto.Name = Console.ReadLine().Trim();
                    break;
                case "-httpversion":
                    Console.Write("Http Version: ");
                    httpRequestContainerDto.HttpRequest.Httpversion = Console.ReadLine().Trim();
                    break;
                case "-httpmethod":
                    Console.Write("Http Method: ");
                    httpRequestContainerDto.HttpRequest.HttpMethod = Console.ReadLine().Trim();
                    break;
                case "-timeout":
                    Console.Write("Timeout: ");
                    try
                    {
                        httpRequestContainerDto.HttpRequest.HttpRequestTimeOut = int.Parse(Console.ReadLine());
                    }
                    catch
                    {
                        httpRequestContainerDto.HttpRequest.HttpRequestTimeOut = -1;
                    }
                    break;
                case "-repeat":
                    Console.Write("Repeat: ");
                    try
                    {
                        httpRequestContainerDto.NumberofAsyncRepeats = int.Parse(Console.ReadLine());
                    }
                    catch
                    {
                        httpRequestContainerDto.NumberofAsyncRepeats = -1;
                    }
                    break;
                case "-url":
                    Console.Write("Url: ");
                    httpRequestContainerDto.HttpRequest.URL = Console.ReadLine().Trim();
                    break;
            }

        }

        //work on this to receive userInputObject(create a child calss)
        static bool ValidateUserInput(string property, HttpAsyncRequestContainer.SetupCommand httpRequestContainerDto)
        {
            string[] httpMethods = { "GET", "HEAD", "POST", "PUT", "PATCH", "DELETE", "CONNECT", "OPTIONS", "TRACE" };

            switch (property)
            {
                case "-requestname":
                    if (string.IsNullOrEmpty(httpRequestContainerDto.Name) || !Regex.IsMatch(httpRequestContainerDto.Name, @"^[\w.-]{2,}$"))
                    {
                        return false;
                    }
                    break;
                case "-httpversion":
                    if (httpRequestContainerDto.HttpRequest.Httpversion != "1.0" && httpRequestContainerDto.HttpRequest.Httpversion != "1.1")
                    {
                        return false;
                    }
                    break;
                case "-httpmethod":
                    if (httpRequestContainerDto.HttpRequest.HttpMethod == null || !httpMethods.Any(httpMethod => httpMethod == httpRequestContainerDto.HttpRequest.HttpMethod.ToUpper()))
                    {
                        return false;
                    }
                    break;
                case "-timeout":
                    if (httpRequestContainerDto.HttpRequest.HttpRequestTimeOut <= 0)
                    {
                        return false;
                    }
                    break;
                case "-repeat":

                    if (httpRequestContainerDto.NumberofAsyncRepeats <= 0)
                    {
                        return false;
                    }
                    break;
                case "-url":

                    if (!(Uri.TryCreate(httpRequestContainerDto.HttpRequest.URL, UriKind.Absolute, out Uri uriResult) && (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps || uriResult.Scheme.Contains("ws"))))
                    {
                        return false;
                    }
                    break;
            }
            return true;
        }

        static bool SetPayload(HttpAsyncRequest.SetupCommand dto, string path)
        {
            try
            {
                dto.Payload = File.ReadAllText(path);
                return true;
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Unable To Read Data From The Specified Path");
                Console.WriteLine(ex.Message);
                Console.ResetColor();
                return false;
            }
        }

        static void SetHeaders(HttpAsyncRequest.SetupCommand dto)
        {
            dto.HttpHeaders = new Dictionary<string, string>();

            Console.WriteLine("Enter the request headers");
            Console.WriteLine("Type your header in the following format (headerName: headerValue) and click enter. After you finish entering all headers, enter done");
            while (true)
            {
                string input = Console.ReadLine().Trim();
                if (input == "done")
                {
                    break;
                }
                try
                {
                    string[] header = input.Split(':');
                    if (!dto.HttpHeaders.ContainsKey(header[0].Trim()))
                        dto.HttpHeaders.Add(header[0].Trim(), header[1].Trim());
                    else
                        dto.HttpHeaders[header[0].Trim()] = header[1].Trim();
                }
                catch
                {
                    Console.WriteLine("Enter header in a valid format e.g (headerName: headerValue) or enter done to start filling th epayload");
                }
            }
        }

        static void ResetProperties()
        {
            requiredProperties = new Dictionary<string, bool>()
            {
                { "-Add", false }, { "-a", false },
                { "-url",false },
                { "-httpmethod",false },{ "-hm",false },
                { "-header",false },{ "-h",false },
                { "-repeat",false }, { "-r",false }
            };

            optionalProperties = new Dictionary<string, bool>()
            {
                { "-name", false },{ "-n", false },
                { "-payload", false },{ "-p", false },
                { "-requestname", false },{ "-rn", false },
                { "-httpversion", false }, { "-hv", false },
                { "-timeout", false },{ "-t", false },
            };
        }

        internal void Run(T1 setupCommand, string [] args)
        {
            var command = (HttpAsyncTest.SetupCommand) ((ICommand<HttpAsyncTest>) setupCommand);
            if (args.Length != 0)
            {
                TestService<T1, T2>.BuildFromCommand(command, args);
            }
            else
            {
                Console.WriteLine("Start building your test manually");
                TestService<T1, T2>.BuildManual(command);
            }

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("...Test has started...");
            Console.ResetColor();

            HttpAsyncTest asyncTest = new HttpAsyncTest(command, _logger);
            await new HttpAsyncTest.ExecuteCommand().ExecuteAsync(asyncTest);

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("...Test has completed...");
            Console.ResetColor();
            string action;
            while (true)
            {
                Console.WriteLine("Press any key to exit, enter \"start over\" to start over or \"redo\" to repeat the same test ");
                action = Console.ReadLine().Trim().ToLower();
                if (action == "redo")
                {
                    await new HttpAsyncTest.RedoCommand().ExecuteAsync(asyncTest);
                    continue;
                }
                break;
            }
            if (action == "start over")
            {
                args = new string[] { };
                await StartAsyncTest(args);
            }
        }
    }

}
