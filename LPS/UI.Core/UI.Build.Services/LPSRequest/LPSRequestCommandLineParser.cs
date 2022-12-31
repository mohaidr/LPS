using LPS.UI.Common;
using LPS.Domain;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Input;

namespace LPS.UI.Core.UI.Build.Services
{
    internal class LPSRequestCommandLineParser : IParser<LPSRequest.SetupCommand, LPSRequest>
    {
        public string[] CommandLineArgs { get; set; }

        public LPSRequestCommandLineParser()
        {
        }

        public void Parse(LPSRequest.SetupCommand command)
        {
            var kvp = new List<string>();
            for (int i = 0; i < CommandLineArgs.Length; i++)
            {
                switch (CommandLineArgs[i])
                {
                    case "-httpversion":
                    case "-hv":
                        command.Httpversion = CommandLineArgs[++i];
                        break;
                    case "-httpmethod":
                    case "-hm":
                        command.HttpMethod = CommandLineArgs[++i];
                        break;
                    case "-timeout":
                    case "-t":
                        try
                        {
                            command.TimeOut = int.Parse(CommandLineArgs[++i]);
                        }
                        catch
                        {
                            command.TimeOut = -1;
                            CommandLineArgs[i] = command.TimeOut.ToString();
                        }
                        break;
                    case "-header":
                    case "-h":
                        kvp = CommandLineArgs[++i].Split(':').ToList<string>();
                        if (kvp.Count >= 2)
                        {
                            kvp = CommandLineArgs[++i].Split(':').ToList<string>();
                            command.HttpHeaders.Add(kvp.First(), string.Join(":", kvp.Where(str => str != kvp.First())));
                        }
                        else
                            throw new ArgumentException("Invalid Header Value");
                        break;
                    case "-url":
                        command.URL = CommandLineArgs[++i];
                        break;
                    case "-payload":
                    case "-p":
                        i++;
                        command.Payload = InputPayloadService.ReadFromFile(CommandLineArgs[i]);
                        bool loop = true;
                        while (string.IsNullOrEmpty(command.Payload) && loop)
                        {
                            command.Payload = InputPayloadService.Challenge();
                        }
                        break;
                  //  default:
                    //    throw new ArgumentException($"{CommandLineArgs[i]} is an invalid argument");
                }
            }
        }
    }
}
