using LPS.Domain;
using LPS.Infrastructure.Common;
using LPS.UI.Common;
using LPS.UI.Common.Extensions;
using LPS.UI.Core.LPSCommandLine.Bindings;
using LPS.UI.Core.LPSValidators;
using LPS.UI.Core.Services;
using System.CommandLine;
using ValidationResult = FluentValidation.Results.ValidationResult;

namespace LPS.UI.Core.LPSCommandLine.Commands
{
    internal class RoundCLICommand : ICLICommand
    {
        private Command _rootCliCommand;
        private string[] _args;
        private Command _roundCommand;
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
        internal RoundCLICommand(Command rootLpsCliCommand, string[] args)
        {
            _rootCliCommand = rootLpsCliCommand;
            _args = args;
            Setup();
        }

        private void Setup()
        {
            _roundCommand = new Command("round", "Create a new test")  
            {
                CommandLineOptions.LPSRoundCommandOptions.ConfigFileArgument // Add ConfigFileArgument here
            };
            CommandLineOptions.AddOptionsToCommand(_roundCommand, typeof(CommandLineOptions.LPSRoundCommandOptions));
            _rootCliCommand.AddCommand(_roundCommand);
        }

        public async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            _roundCommand.SetHandler((configFile, round) =>
            {
                bool isValidRound;
                ValidationResult results;
                var roundValidator = new RoundValidator(round);
                results = roundValidator.Validate();
                isValidRound = results.IsValid;
                if (!isValidRound)
                {
                    results.PrintValidationErrors();
                }
                else
                {
                    Plan.SetupCommand setupCommand = ConfigurationService.FetchConfiguration(configFile);
                    var selectedRound = setupCommand.Rounds.FirstOrDefault(r => r.Name == round.Name);
                    if (selectedRound != null)
                    {
                        selectedRound = round.Clone();
                    }
                    else
                    {
                        setupCommand.Rounds.Add(round);
                    }
                    ConfigurationService.SaveConfiguration(configFile, setupCommand);
                }
            },
            CommandLineOptions.LPSRoundCommandOptions.ConfigFileArgument,
            new RoundCommandBinder());
            await _rootCliCommand.InvokeAsync(_args);
        }
    }
}
