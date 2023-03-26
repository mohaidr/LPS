using LPS.Domain;
using System;
using System.Collections.Generic;
using System.Linq;
using System.CommandLine;
using System.IO;
using Newtonsoft.Json;
using System.CommandLine.Binding;
using LPS.Domain.Common;
using LPS.UI.Common;

namespace LPS.UI.Core.UI.Build.Services
{
    public class CommandLineParser: IParser<LPSTest.SetupCommand, LPSTest>
    {
        public string[] CommandLineArgs { get; set; }

        ICustomLogger _logger;
        LPSTest.SetupCommand _command;
        public CommandLineParser(ICustomLogger logger, LPSTest.SetupCommand command)
        {
            _logger = logger;
            _command = command;
        }
        public LPSTest.SetupCommand Command { get { return _command; } set { } }

        public void Parse()
        {
            var lpsCommand = new Command("lps", "Load, Performance and Stress Testing Command Tool.");

            Command createCommand = new Command("create", "Create a new test") {
                CommandLineOptions.TestNameOption
            };
            Command addCommand = new Command("add", "Add an http request")
            {
                CommandLineOptions.TestNameOption,
                CommandLineOptions.NameOption,
                CommandLineOptions.HttpMethodOption,
                CommandLineOptions.HttpversionOption,
                CommandLineOptions.TimeoutOption,
                CommandLineOptions.RepeatOption,
                CommandLineOptions.UrlOption,
                CommandLineOptions.HeaderOption,
                CommandLineOptions.PayloadOption
            };
            Command runCommand = new Command("run", "Run existing test") {
               CommandLineOptions.TestNameOption,
            };

            lpsCommand.AddCommand(createCommand);
            lpsCommand.AddCommand(addCommand);
            lpsCommand.AddCommand(runCommand);

            createCommand.SetHandler((testName) =>
                {
                    _command.Name = testName;
                    string json = new LpsSerializer().Serialize(_command);
                    File.WriteAllText($"{testName}.json", json);
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine("Test Has Been Created Successfully");
                    Console.ResetColor();

                },
                CommandLineOptions.TestNameOption);

            addCommand.SetHandler(
                (testName, lpsRequestWrapper) =>
                {
                    var serializer = new LpsSerializer();
                    _command = serializer.DeSerialize(File.ReadAllText($"{testName}.json"));
                    _command.LPSRequestWrappers.Add(lpsRequestWrapper);
                    _command.IsValid = true;
                    string json = serializer.Serialize(_command);
                    File.WriteAllText($"{testName}.json", json);
                    Console.ForegroundColor= ConsoleColor.Green;
                    Console.WriteLine("Request Has Been Added Successfully");
                    Console.ResetColor();

                },
                CommandLineOptions.TestNameOption,
                new LPSRequestWrapperBinder(
                CommandLineOptions.NameOption,
                CommandLineOptions.RepeatOption,
                CommandLineOptions.HttpMethodOption,
                CommandLineOptions.HttpversionOption,
                CommandLineOptions.TimeoutOption,
                CommandLineOptions.UrlOption,
                CommandLineOptions.HeaderOption,
                CommandLineOptions.PayloadOption));

            runCommand.SetHandler(async (testName) =>
            {
                _command = new LpsSerializer().DeSerialize(File.ReadAllText($"{testName}.json"));
                await new LpsRunner().Run(_command, _logger);
            }, CommandLineOptions.TestNameOption);

            lpsCommand.Invoke(CommandLineArgs);
        }
    }
}
