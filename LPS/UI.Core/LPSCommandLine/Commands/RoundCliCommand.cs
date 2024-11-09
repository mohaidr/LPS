using LPS.Domain;
using LPS.DTOs;
using LPS.Infrastructure.Common;
using LPS.UI.Common;
using LPS.UI.Common.Extensions;
using LPS.UI.Core.LPSCommandLine.Bindings;
using LPS.UI.Core.LPSValidators;
using LPS.UI.Core.Services;
using System.CommandLine;
using System.Xml.Linq;
using ValidationResult = FluentValidation.Results.ValidationResult;

namespace LPS.UI.Core.LPSCommandLine.Commands
{
    internal class RoundCliCommand : ICliCommand
    {
        private Command _rootCliCommand;
        private string[] _args;
        private Command _roundCommand;
        public Command Command => _roundCommand;
        #pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
        internal RoundCliCommand(Command rootLpsCliCommand, string[] args)
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

        public void SetHandler(CancellationToken cancellationToken)
        {
            _roundCommand.SetHandler((configFile, round) =>
            {
                ValidationResult results;
                var roundValidator = new RoundValidator(round);
                results = roundValidator.Validate();
                if (!results.IsValid)
                {
                    results.PrintValidationErrors();
                }
                else
                {
                    PlanDto planDto = ConfigurationService.FetchConfiguration<PlanDto>(configFile);
                    var selectedRound = planDto?.Rounds.FirstOrDefault(r => r.Name == round.Name);
                    if (selectedRound != null)
                    {
                        selectedRound.Name = round.Name;
                        selectedRound.StartupDelay = round.StartupDelay;
                        selectedRound.NumberOfClients = round.NumberOfClients;
                        selectedRound.ArrivalDelay = round.ArrivalDelay;
                        selectedRound.DelayClientCreationUntilIsNeeded = round.DelayClientCreationUntilIsNeeded;
                        selectedRound.RunInParallel = round.RunInParallel;
                    }
                    else
                    {
                        planDto?.Rounds.Add(round);
                    }
                    ConfigurationService.SaveConfiguration(configFile, planDto);
                }
            },
            CommandLineOptions.LPSRoundCommandOptions.ConfigFileArgument,
            new RoundCommandBinder());
        }
    }
}
