using FluentValidation;
using FluentValidation.Results;
using LPS.Domain;
using LPS.Infrastructure.Common;
using LPS.UI.Common;
using LPS.UI.Common.Extensions;
using LPS.UI.Core.LPSCommandLine.Bindings;
using LPS.UI.Core.LPSValidators;
using LPS.UI.Core.Services;
using Spectre.Console;
using System;
using System.CommandLine;

using ValidationResult = FluentValidation.Results.ValidationResult;

namespace LPS.UI.Core.LPSCommandLine.Commands
{
    internal class IterationCLICommand: ICLICommand
    {
        private Command _rootCliCommand;
        private Command _iterationCommand;
        private string[] _args;
        internal IterationCLICommand(Command rootCliCommand, string[] args)
        {
            _rootCliCommand = rootCliCommand;
            _args = args;
            Setup();
        }

        private void Setup()
        {
            _iterationCommand = new Command("iteration", "Add an http iteration")
            {
                CommandLineOptions.LPSIterationCommandOptions.ConfigFileArgument // Add ConfigFileArgument here
            };
            CommandLineOptions.AddOptionsToCommand(_iterationCommand, typeof(CommandLineOptions.LPSIterationCommandOptions));
            _rootCliCommand.AddCommand(_iterationCommand);
        }

        public async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            _iterationCommand.SetHandler((configFile, roundName, iteration) =>
            {
                var itrationValidator = new IterationValidator(iteration);
                ValidationResult results = itrationValidator.Validate();

                if (results.IsValid)
                {
                    try
                    {
                        var setupCommand = ConfigurationService.FetchConfiguration(configFile);
                        var round = setupCommand.Rounds.FirstOrDefault(r => r.Name == roundName);
                        if (round != null)
                        {
                            var selectedIteration = round.Iterations.FirstOrDefault(i => i.Name == iteration.Name);
                            if (selectedIteration != null)
                            {
                                selectedIteration.Name = iteration.Name;
                                selectedIteration.MaximizeThroughput = iteration.MaximizeThroughput;
                                selectedIteration.Mode = iteration.Mode;
                                selectedIteration.RequestCount = iteration.RequestCount;
                                selectedIteration.Duration = iteration.Duration;
                                selectedIteration.BatchSize = iteration.BatchSize;
                                selectedIteration.CoolDownTime = iteration.CoolDownTime;
                                selectedIteration.RequestProfile.URL = iteration.RequestProfile.URL;
                                selectedIteration.RequestProfile.HttpMethod = iteration.RequestProfile.HttpMethod;
                                selectedIteration.RequestProfile.HttpVersion = iteration.RequestProfile.HttpVersion;
                                selectedIteration.RequestProfile.Payload = iteration.RequestProfile.Payload;
                                selectedIteration.RequestProfile.DownloadHtmlEmbeddedResources = iteration.RequestProfile.DownloadHtmlEmbeddedResources;
                                selectedIteration.RequestProfile.SaveResponse = iteration.RequestProfile.SaveResponse;
                                selectedIteration.RequestProfile.SupportH2C = iteration.RequestProfile.SupportH2C;
                                selectedIteration.RequestProfile.HttpHeaders = new Dictionary<string, string>(iteration.RequestProfile.HttpHeaders);
                            }
                            else
                            {
                                Console.WriteLine("adding");
                                round.Iterations.Add(iteration);
                            }
                            ConfigurationService.SaveConfiguration(configFile, setupCommand);
                        }
                        else
                        {
                            throw new ArgumentException($"Invalid Round Name {roundName}");
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex.Message);
                    }
                }
                else {
                    results.PrintValidationErrors();
                }

            },
            CommandLineOptions.LPSIterationCommandOptions.ConfigFileArgument,
            CommandLineOptions.LPSIterationCommandOptions.RoundName,
            new IterationCommandBinder());
            await _rootCliCommand.InvokeAsync(_args);
        }
    }
}
