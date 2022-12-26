using AsyncCalls.UI.Common;
using AsyncTest.Domain;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Input;

namespace AsyncCalls.UI.Core.UI.Build.Services
{
    internal class CommandLineParser : IParser<HttpAsyncTest.SetupCommand, HttpAsyncTest>
    {
        public string[] CommandLineArgs { get; set; }
        private Dictionary<string, bool> _requiredProperties;
        private Dictionary<string, bool> _optionalProperties;
        HttpAsyncRequestWrapperValidator _validator;
        string[] requiredPropsAsArray;

        public CommandLineParser(HttpAsyncRequestWrapperValidator validator)
        {
            _requiredProperties = CommandProps.GetRequiredProperties();
            _optionalProperties = CommandProps.GetOptionalProperties();
            requiredPropsAsArray = _requiredProperties.Keys.ToArray();
            _validator = validator;
        }

        public void Parse(HttpAsyncTest.SetupCommand setupCommand)
        {

            int numberofRequests = CommandLineArgs.Count(arg => (arg == "-add" || arg == "-a"));

            if (numberofRequests == 0)
            {
                throw new ArgumentException("-Add property is required");
            }

            if (CommandLineArgs.Count(arg => arg != "-add") % 2 != 0)
                throw new ArgumentException("A valid property should start with - and followed by its value");

            CommandLineArgs = Array.ConvertAll(CommandLineArgs, arg => arg.ToLower());

            setupCommand.Name = ((CommandLineArgs[0] == "-name" || CommandLineArgs[0] == "-n") && !string.IsNullOrEmpty(CommandLineArgs[0])) ? CommandLineArgs[0] : DateTime.Now.ToFileTime().ToString();
            HttpAsyncRequestWrapper.SetupCommand httpRequestWrapperCommand = new HttpAsyncRequestWrapper.SetupCommand();

            string[] kvp = new string[2];
            setupCommand.IsCommandLine = true;

            for (int i = 0; i < CommandLineArgs.Length; i++)
            {
                switch (CommandLineArgs[i])
                {
                    case "-add":
                    case "-a":
                        if (i > 2)
                        {
                            //Force user to enter empty required arguments
                            ForceRequiredCommandArguments(httpRequestWrapperCommand);
                        }
                        CommandProps.ResetProperties(_requiredProperties, _optionalProperties);
                        _requiredProperties["-add"] = true;
                        _requiredProperties["-a"] = true;
                        httpRequestWrapperCommand = new HttpAsyncRequestWrapper.SetupCommand();
                        ChallengeService.SetOptionalFeildsToDefaultValues(httpRequestWrapperCommand);
                        setupCommand.HttpRequestWrappers.Add(httpRequestWrapperCommand);
                        break;
                    case "-requestname":
                    case "-rn":
                        httpRequestWrapperCommand.Name = CommandLineArgs[++i];
                        if (!_validator.Validate("-requestname", httpRequestWrapperCommand))
                        {
                            httpRequestWrapperCommand.Name = ChallengeService.Challenge("-requestname");
                            CommandLineArgs[i] = httpRequestWrapperCommand.Name;
                            i -= 2;
                            continue;
                        }
                        _optionalProperties["-requestname"] = true;
                        _optionalProperties["-rn"] = true;
                        break;
                    case "-httpversion":
                    case "-hv":
                        httpRequestWrapperCommand.HttpRequest.Httpversion = CommandLineArgs[++i];
                        if (!_validator.Validate("-httpversion", httpRequestWrapperCommand))
                        {
                            httpRequestWrapperCommand.HttpRequest.Httpversion = ChallengeService.Challenge("-httpversion");
                            CommandLineArgs[i] = httpRequestWrapperCommand.HttpRequest.Httpversion;
                            i -= 2;
                            continue;
                        }
                        _optionalProperties["-httpversion"] = true;
                        _optionalProperties["-hv"] = true;
                        break;
                    case "-httpmethod":
                    case "-hm":
                        httpRequestWrapperCommand.HttpRequest.HttpMethod = CommandLineArgs[++i];
                        if (!_validator.Validate("-httpmethod", httpRequestWrapperCommand))
                        {
                            httpRequestWrapperCommand.HttpRequest.HttpMethod = ChallengeService.Challenge("-httpmethod");
                            CommandLineArgs[i] = httpRequestWrapperCommand.HttpRequest.HttpMethod;
                            i -= 2;
                            continue;
                        }
                        _requiredProperties["-httpmethod"] = true;
                        _requiredProperties["-hm"] = true;
                        break;
                    case "-timeout":
                    case "-t":
                        try
                        {
                            httpRequestWrapperCommand.HttpRequest.TimeOut = int.Parse(CommandLineArgs[++i]);
                        }
                        catch
                        {
                            httpRequestWrapperCommand.HttpRequest.TimeOut = -1;
                            CommandLineArgs[i] = httpRequestWrapperCommand.HttpRequest.TimeOut.ToString();
                        }
                        if (!_validator.Validate("-timeout", httpRequestWrapperCommand))
                        {
                            try
                            {
                                httpRequestWrapperCommand.HttpRequest.TimeOut = int.Parse(ChallengeService.Challenge("-timeout"));
                            }
                            catch { }
                            CommandLineArgs[i] = httpRequestWrapperCommand.HttpRequest.TimeOut.ToString();
                            i -= 2;
                            continue;
                        }
                        _optionalProperties["-timeout"] = true;
                        _optionalProperties["-t"] = true;
                        break;
                    case "-repeat":
                    case "-r":
                        try
                        {
                            httpRequestWrapperCommand.NumberofAsyncRepeats = int.Parse(CommandLineArgs[++i]);
                        }
                        catch
                        {
                            httpRequestWrapperCommand.NumberofAsyncRepeats = -1;
                            CommandLineArgs[i] = httpRequestWrapperCommand.NumberofAsyncRepeats.ToString();
                        }
                        if (!_validator.Validate("-repeat", httpRequestWrapperCommand))
                        {
                            try
                            {
                                httpRequestWrapperCommand.NumberofAsyncRepeats = int.Parse(ChallengeService.Challenge("-repeat"));
                            }
                            catch { }
                            CommandLineArgs[i] = httpRequestWrapperCommand.NumberofAsyncRepeats.ToString();
                            i -= 2;
                            continue;
                        }
                        _requiredProperties["-repeat"] = true;
                        _requiredProperties["-r"] = true;
                        break;
                    case "-header":
                    case "-h":
                        kvp = CommandLineArgs[++i].Split(':');
                        if (kvp.Length == 2)
                            httpRequestWrapperCommand.HttpRequest.HttpHeaders.Add(kvp[0], kvp[1]);
                        else
                            throw new ArgumentException("Invalid Header Value");
                        _requiredProperties["-header"] = true;
                        _requiredProperties["-h"] = true;
                        break;
                    case "-url":
                        httpRequestWrapperCommand.HttpRequest.URL = CommandLineArgs[++i];
                        if (!_validator.Validate("-url", httpRequestWrapperCommand))
                        {
                            httpRequestWrapperCommand.HttpRequest.URL = ChallengeService.Challenge("-url");
                            CommandLineArgs[i] = httpRequestWrapperCommand.HttpRequest.URL;
                            i -= 2;
                            continue;
                        }
                        _requiredProperties["-url"] = true;
                        break;
                    case "-payload":
                    case "-p":
                        i++;
                        httpRequestWrapperCommand.HttpRequest.Payload = InputPayloadService.ReadFromFile(CommandLineArgs[i]);
                        bool loop = true;
                        while (string.IsNullOrEmpty(httpRequestWrapperCommand.HttpRequest.Payload) && loop)
                        {
                            httpRequestWrapperCommand.HttpRequest.Payload = InputPayloadService.Challenge();
                        }
                        _optionalProperties["-payload"] = true;
                        _optionalProperties["-p"] = true;
                        break;
                    default:
                        throw new ArgumentException($"{CommandLineArgs[i]} is an invalid argument");
                }
            }
            ForceRequiredCommandArguments(httpRequestWrapperCommand);
        }

        public void ForceRequiredCommandArguments(HttpAsyncRequestWrapper.SetupCommand httpRequestWrapperCommand)
        {
            while (true)
            {

                if (!_validator.Validate("-httpmethod", httpRequestWrapperCommand))
                {
                    Console.WriteLine("Enter a Valid Http Method");
                    httpRequestWrapperCommand.HttpRequest.HttpMethod = ChallengeService.Challenge("-httpmethod");
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
                    catch {
                        continue;
                    }
                }
                break;
            }
        }
    }
}
