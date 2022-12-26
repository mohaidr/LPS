using LPS.UI.Common;
using LPS.Domain;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Input;

namespace LPS.UI.Core.UI.Build.Services
{
    internal class CommandLineParser : IParser<LPSTest.SetupCommand, LPSTest>
    {
        public string[] CommandLineArgs { get; set; }
        private Dictionary<string, bool> _requiredProperties;
        private Dictionary<string, bool> _optionalProperties;
        LPSRequestWrapperValidator _validator;
        string[] requiredPropsAsArray;

        public CommandLineParser(LPSRequestWrapperValidator validator)
        {
            _requiredProperties = CommandProps.GetRequiredProperties();
            _optionalProperties = CommandProps.GetOptionalProperties();
            requiredPropsAsArray = _requiredProperties.Keys.ToArray();
            _validator = validator;
        }

        public void Parse(LPSTest.SetupCommand setupCommand)
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
            LPSRequestWrapper.SetupCommand lpsRequestWrapperCommand = new LPSRequestWrapper.SetupCommand();

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
                            ForceRequiredCommandArguments(lpsRequestWrapperCommand);
                        }
                        CommandProps.ResetProperties(_requiredProperties, _optionalProperties);
                        _requiredProperties["-add"] = true;
                        _requiredProperties["-a"] = true;
                        lpsRequestWrapperCommand = new LPSRequestWrapper.SetupCommand();
                        ChallengeService.SetOptionalFeildsToDefaultValues(lpsRequestWrapperCommand);
                        setupCommand.lpsRequestWrappers.Add(lpsRequestWrapperCommand);
                        break;
                    case "-requestname":
                    case "-rn":
                        lpsRequestWrapperCommand.Name = CommandLineArgs[++i];
                        if (!_validator.Validate("-requestname", lpsRequestWrapperCommand))
                        {
                            lpsRequestWrapperCommand.Name = ChallengeService.Challenge("-requestname");
                            CommandLineArgs[i] = lpsRequestWrapperCommand.Name;
                            i -= 2;
                            continue;
                        }
                        _optionalProperties["-requestname"] = true;
                        _optionalProperties["-rn"] = true;
                        break;
                    case "-httpversion":
                    case "-hv":
                        lpsRequestWrapperCommand.LPSRequest.Httpversion = CommandLineArgs[++i];
                        if (!_validator.Validate("-httpversion", lpsRequestWrapperCommand))
                        {
                            lpsRequestWrapperCommand.LPSRequest.Httpversion = ChallengeService.Challenge("-httpversion");
                            CommandLineArgs[i] = lpsRequestWrapperCommand.LPSRequest.Httpversion;
                            i -= 2;
                            continue;
                        }
                        _optionalProperties["-httpversion"] = true;
                        _optionalProperties["-hv"] = true;
                        break;
                    case "-httpmethod":
                    case "-hm":
                        lpsRequestWrapperCommand.LPSRequest.HttpMethod = CommandLineArgs[++i];
                        if (!_validator.Validate("-httpmethod", lpsRequestWrapperCommand))
                        {
                            lpsRequestWrapperCommand.LPSRequest.HttpMethod = ChallengeService.Challenge("-httpmethod");
                            CommandLineArgs[i] = lpsRequestWrapperCommand.LPSRequest.HttpMethod;
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
                            lpsRequestWrapperCommand.LPSRequest.TimeOut = int.Parse(CommandLineArgs[++i]);
                        }
                        catch
                        {
                            lpsRequestWrapperCommand.LPSRequest.TimeOut = -1;
                            CommandLineArgs[i] = lpsRequestWrapperCommand.LPSRequest.TimeOut.ToString();
                        }
                        if (!_validator.Validate("-timeout", lpsRequestWrapperCommand))
                        {
                            try
                            {
                                lpsRequestWrapperCommand.LPSRequest.TimeOut = int.Parse(ChallengeService.Challenge("-timeout"));
                            }
                            catch { }
                            CommandLineArgs[i] = lpsRequestWrapperCommand.LPSRequest.TimeOut.ToString();
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
                            lpsRequestWrapperCommand.NumberofAsyncRepeats = int.Parse(CommandLineArgs[++i]);
                        }
                        catch
                        {
                            lpsRequestWrapperCommand.NumberofAsyncRepeats = -1;
                            CommandLineArgs[i] = lpsRequestWrapperCommand.NumberofAsyncRepeats.ToString();
                        }
                        if (!_validator.Validate("-repeat", lpsRequestWrapperCommand))
                        {
                            try
                            {
                                lpsRequestWrapperCommand.NumberofAsyncRepeats = int.Parse(ChallengeService.Challenge("-repeat"));
                            }
                            catch { }
                            CommandLineArgs[i] = lpsRequestWrapperCommand.NumberofAsyncRepeats.ToString();
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
                            lpsRequestWrapperCommand.LPSRequest.HttpHeaders.Add(kvp[0], kvp[1]);
                        else
                            throw new ArgumentException("Invalid Header Value");
                        _requiredProperties["-header"] = true;
                        _requiredProperties["-h"] = true;
                        break;
                    case "-url":
                        lpsRequestWrapperCommand.LPSRequest.URL = CommandLineArgs[++i];
                        if (!_validator.Validate("-url", lpsRequestWrapperCommand))
                        {
                            lpsRequestWrapperCommand.LPSRequest.URL = ChallengeService.Challenge("-url");
                            CommandLineArgs[i] = lpsRequestWrapperCommand.LPSRequest.URL;
                            i -= 2;
                            continue;
                        }
                        _requiredProperties["-url"] = true;
                        break;
                    case "-payload":
                    case "-p":
                        i++;
                        lpsRequestWrapperCommand.LPSRequest.Payload = InputPayloadService.ReadFromFile(CommandLineArgs[i]);
                        bool loop = true;
                        while (string.IsNullOrEmpty(lpsRequestWrapperCommand.LPSRequest.Payload) && loop)
                        {
                            lpsRequestWrapperCommand.LPSRequest.Payload = InputPayloadService.Challenge();
                        }
                        _optionalProperties["-payload"] = true;
                        _optionalProperties["-p"] = true;
                        break;
                    default:
                        throw new ArgumentException($"{CommandLineArgs[i]} is an invalid argument");
                }
            }
            ForceRequiredCommandArguments(lpsRequestWrapperCommand);
        }

        public void ForceRequiredCommandArguments(LPSRequestWrapper.SetupCommand lpsRequestWrapperCommand)
        {
            while (true)
            {

                if (!_validator.Validate("-httpmethod", lpsRequestWrapperCommand))
                {
                    Console.WriteLine("Enter a Valid Http Method");
                    lpsRequestWrapperCommand.LPSRequest.HttpMethod = ChallengeService.Challenge("-httpmethod");
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
                break;
            }
        }
    }
}
