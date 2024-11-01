using System;
using System.Collections.Generic;
using System.CommandLine;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using LPS.Domain;
using LPS.UI.Common;
using LPS.UI.Core.LPSValidators;
using Spectre.Console;

namespace LPS.UI.Core.Build.Services
{
    internal class RoundChallengeUserService(bool skipOptionalFields, Round.SetupCommand command, IBaseValidator<Round.SetupCommand, Round> validator) : IChallengeUserService<Round.SetupCommand, Round>
    {
        IBaseValidator<Round.SetupCommand, Round> _validator = validator;
        Round.SetupCommand _command = command;
        public Round.SetupCommand Command => _command;
        public bool SkipOptionalFields  => _skipOptionalFields;

        private bool _skipOptionalFields = skipOptionalFields;

        public void Challenge()
        {
            if (!_skipOptionalFields)
            {
                ResetOptionalFields();
            }
            AnsiConsole.MarkupLine("[underline bold blue]Create a Test Round:[/]");
            while (true)
            {
                if (!_validator.Validate(nameof(Command.Name)))
                {
                    _validator.PrintValidationErrors(nameof(Command.Name));
                    _command.Name = AnsiConsole.Ask<string>("What's your [green]'Round Name'[/]?");
                    continue;
                }

                if (!_validator.Validate(nameof(Command.StartupDelay)))
                {
                    _validator.PrintValidationErrors(nameof(Command.StartupDelay));
                    _command.StartupDelay = AnsiConsole.Ask<int>("Would you like to add a [green]'Startup Delay'[/]? Enter 0 if not.");
                    continue;
                }

                if (!_validator.Validate(nameof(Command.NumberOfClients)))
                {
                    _validator.PrintValidationErrors(nameof(Command.NumberOfClients));
                    _command.NumberOfClients = AnsiConsole.Ask<int>("How [green]'Many Clients'[/] should run your test?");
                    continue;
                }

                if (!_validator.Validate(nameof(Command.ArrivalDelay)))
                {
                    if (Command.NumberOfClients == 1)
                    {
                        _command.ArrivalDelay = 1;
                        continue;
                    }
                    _validator.PrintValidationErrors(nameof(Command.ArrivalDelay));
                    _command.ArrivalDelay = AnsiConsole.Ask<int>("What is the [green]'Client Arrival Interval'[/] in milliseconds (i.e., the delay between each client's arrival)?");
                    continue;
                }

                if (!_validator.Validate(nameof(Command.DelayClientCreationUntilIsNeeded)))
                {
                    _validator.PrintValidationErrors(nameof(Command.DelayClientCreationUntilIsNeeded));
                    _command.DelayClientCreationUntilIsNeeded = AnsiConsole.Confirm("Do you want to [green]'Delay'[/] the client creation until is needed?");
                    continue;
                }

                if (!_validator.Validate(nameof(Command.RunInParallel)))
                {
                    _validator.PrintValidationErrors(nameof(Command.RunInParallel));
                    _command.RunInParallel = AnsiConsole.Confirm("Do you want to run all your http iterations in [green]'Parallel'[/]?");
                    continue;
                }

                HttpIteration.SetupCommand iterationCommand = new HttpIteration.SetupCommand();
                IterationValidator validator = new IterationValidator(iterationCommand);
                IterationChallengeUserService iterationUserService = new(SkipOptionalFields, iterationCommand, validator);
                iterationUserService.Challenge();

                Command.Iterations.Add(iterationCommand);

                AnsiConsole.MarkupLine("[bold]Type [blue]add[/] to add a new http iteration or press [blue]enter[/] [/]");

                string? action = Console.ReadLine()?.Trim().ToLower();
                if (action == "add")
                {
                    continue;
                }
                break;
            }

            _command.IsValid = true;
        }

        public void ResetOptionalFields()
        {
            if (!_skipOptionalFields)
            {
                _command.StartupDelay = -1;
                _command.DelayClientCreationUntilIsNeeded = null;
                _command.RunInParallel = null;
            }
        }
    }
}
