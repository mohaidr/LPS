using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using LPS.Domain;
using LPS.UI.Common;
using LPS.UI.Core.LPSValidators;
using Spectre.Console;

namespace LPS.UI.Core.UI.Build.Services
{
    internal class LPSTestPlanChallengeUserService : IChallengeUserService<LPSTestPlan.SetupCommand, LPSTestPlan>
    {
        ILPSBaseValidator<LPSTestPlan.SetupCommand, LPSTestPlan> _validator;
        LPSTestPlan.SetupCommand _command;
        public LPSTestPlan.SetupCommand Command { get { return _command; } set { value = _command; } }
        public bool SkipOptionalFields { get { return _skipOptionalFields; } set { value = _skipOptionalFields; } }

        private bool _skipOptionalFields;
        public LPSTestPlanChallengeUserService(bool skipOptionalFields, LPSTestPlan.SetupCommand command, ILPSBaseValidator<LPSTestPlan.SetupCommand, LPSTestPlan> validator)
        {
            _skipOptionalFields = skipOptionalFields;
            _command = command;
            _validator = validator;
        }

        public void Challenge()
        {
            if (!_skipOptionalFields)
            {
                ResetOptionalFields();
            }
            AnsiConsole.MarkupLine("[underline bold blue]Create a Test Plan:[/]");
            while (true)
            {
                if (!_validator.Validate(nameof(Command.Name)))
                {
                    _validator.PrintValidationErrors(nameof(Command.Name));
                    _command.Name = AnsiConsole.Ask<string>("What's your [green]test name[/]?");
                    continue;
                }

                if (!_validator.Validate(nameof(Command.NumberOfClients)))
                {
                    _validator.PrintValidationErrors(nameof(Command.NumberOfClients));
                    _command.NumberOfClients = AnsiConsole.Ask<int>("How many [green]clients[/] should run your test?") ;
                    continue;
                }

                if (!_validator.Validate(nameof(Command.RampUpPeriod)))
                {
                    _validator.PrintValidationErrors(nameof(Command.RampUpPeriod));
                    _command.RampUpPeriod = AnsiConsole.Ask<int>("What is the [green]Ramp Up Period (Milliseconds)[/] between the clients?");
                    continue;
                }

                if (!_validator.Validate(nameof(Command.DelayClientCreationUntilIsNeeded)))
                {
                    _validator.PrintValidationErrors(nameof(Command.DelayClientCreationUntilIsNeeded));
                    _command.DelayClientCreationUntilIsNeeded = AnsiConsole.Confirm("Do you want to delay the client creation until is needed?");
                    continue;
                }

                if (!_validator.Validate(nameof(Command.RunInParallel)))
                {
                    _validator.PrintValidationErrors(nameof(Command.RunInParallel));
                    _command.RunInParallel = AnsiConsole.Confirm("Do you want to run all your http runs in parallel?");
                    continue;
                }

                LPSHttpRun.SetupCommand lpsRunCommand = new LPSHttpRun.SetupCommand();
                LPSRunValidator validator = new LPSRunValidator(lpsRunCommand);
                LPSRunChallengeUserService lpsRunUserService = new LPSRunChallengeUserService(SkipOptionalFields, lpsRunCommand, validator);
                lpsRunUserService.Challenge();

                Command.LPSHttpRuns.Add(lpsRunCommand);

                AnsiConsole.MarkupLine("[bold]Type [blue]add[/] to add a new http run or press [blue]enter[/] [/]");

                string action = Console.ReadLine().Trim().ToLower();
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
                _command.DelayClientCreationUntilIsNeeded = null;
                _command.RunInParallel = null;
            }
        }
    }
}
