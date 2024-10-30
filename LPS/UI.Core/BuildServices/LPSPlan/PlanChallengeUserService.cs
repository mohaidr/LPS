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
    internal class PlanChallengeUserService(bool skipOptionalFields, 
        Plan.SetupCommand command,
        IBaseValidator<Plan.SetupCommand, Plan> validator) : IChallengeUserService<Plan.SetupCommand, Plan>
    {
        IBaseValidator<Plan.SetupCommand, Plan> _validator = validator;
        readonly Plan.SetupCommand _command = command;
        public Plan.SetupCommand Command => _command;
        public bool SkipOptionalFields => _skipOptionalFields;
        private readonly bool _skipOptionalFields = skipOptionalFields;

        public void Challenge()
        {
            if (!_skipOptionalFields)
            {
                ResetOptionalFields();
            }
            AnsiConsole.MarkupLine("[underline bold blue]Create a Plan:[/]");
            while (true)
            {
                if (!_validator.Validate(nameof(Command.Name)))
                {
                    _validator.PrintValidationErrors(nameof(Command.Name));
                    _command.Name = AnsiConsole.Ask<string>("What's your [green]'Plan Name'[/]?");
                    continue;
                }

                Round.SetupCommand lpsRoundCommand = new();
                RoundValidator validator = new(lpsRoundCommand);
                RoundChallengeUserService lpsRoundUserService = new(SkipOptionalFields, lpsRoundCommand, validator);
                lpsRoundUserService.Challenge();

                Command.Rounds.Add(lpsRoundCommand);

                AnsiConsole.MarkupLine("[bold]Type [blue]add[/] to add a new http Round or press [blue]enter[/] [/]");

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
            }
        }
    }
}
