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
                CommandLineOptions.LPSRoundCommandOptions.ConfigFileArgument // Add ConfigFileArgument here
            };
            CommandLineOptions.AddOptionsToCommand(_iterationCommand, typeof(CommandLineOptions.LPSIterationCommandOptions));
            _rootCliCommand.AddCommand(_iterationCommand);
        }

        public async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            _iterationCommand.SetHandler((configFilePath, roundName, iteration) =>
            {
                var itrationValidator = new IterationValidator(iteration);
                ValidationResult results = itrationValidator.Validate();

                if (results.IsValid)
                {
                    try
                    {
                        var setupCommand = ConfigurationService.FetchConfiguration(configFilePath);
                        var round = setupCommand.Rounds.FirstOrDefault(r => r.Name == roundName);
                        if (round != null)
                        {
                            var selectedIteration = round.Iterations.FirstOrDefault(i => i.Name == iteration.Name);
                            if (selectedIteration != null)
                            {
                                selectedIteration = iteration.Clone();
                            }
                            else
                            {
                                round.Iterations.Add(iteration);
                            }
                        }
                        else {
                            throw new ArgumentException($"Invalid Round Name {roundName}");
                        }
                    }
                    catch (Exception ex)
                    { 
                        
                    }
                }

            },
            CommandLineOptions.LPSIterationCommandOptions.ConfigFileArgument,
            CommandLineOptions.LPSIterationCommandOptions.RoundName,
            new IterationCommandBinder());
            await _rootCliCommand.InvokeAsync(_args);
        }
    }
}
