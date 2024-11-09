using System;
using System.Collections.Generic;
using System.CommandLine;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using LPS.Domain;
using LPS.DTOs;
using LPS.UI.Common;
using LPS.UI.Core.LPSValidators;
using Spectre.Console;

namespace LPS.UI.Core.Build.Services
{
    internal class RoundChallengeUserService(bool skipOptionalFields, RoundDto command, IBaseValidator<RoundDto, Round> validator) : IChallengeUserService<RoundDto, Round>
    {
        IBaseValidator<RoundDto, Round> _validator = validator;
        RoundDto _roundDto = command;
        public RoundDto Dto => _roundDto;
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
                if (!_validator.Validate(nameof(Dto.Name)) || !_validator.Validate(nameof(Dto.Iterations)))
                {
                    _validator.PrintValidationErrors(nameof(Dto.Name));
                    _validator.PrintValidationErrors(nameof(Dto.Iterations));
                    _roundDto.Name = AnsiConsole.Ask<string>("What's your [green]'Round Name'[/]?");
                    continue;
                }

                if (!_validator.Validate(nameof(Dto.StartupDelay)))
                {
                    _validator.PrintValidationErrors(nameof(Dto.StartupDelay));
                    _roundDto.StartupDelay = AnsiConsole.Ask<int>("Would you like to add a [green]'Startup Delay'[/]? Enter 0 if not.");
                    continue;
                }

                if (!_validator.Validate(nameof(Dto.NumberOfClients)))
                {
                    _validator.PrintValidationErrors(nameof(Dto.NumberOfClients));
                    _roundDto.NumberOfClients = AnsiConsole.Ask<int>("How [green]'Many Clients'[/] should run your test?");
                    continue;
                }

                if (!_validator.Validate(nameof(Dto.ArrivalDelay)))
                {
                    if (Dto.NumberOfClients == 1)
                    {
                        _roundDto.ArrivalDelay = 1;
                        continue;
                    }
                    _validator.PrintValidationErrors(nameof(Dto.ArrivalDelay));
                    _roundDto.ArrivalDelay = AnsiConsole.Ask<int>("What is the [green]'Client Arrival Interval'[/] in milliseconds (i.e., the delay between each client's arrival)?");
                    continue;
                }

                if (!_validator.Validate(nameof(Dto.DelayClientCreationUntilIsNeeded)))
                {
                    _validator.PrintValidationErrors(nameof(Dto.DelayClientCreationUntilIsNeeded));
                    _roundDto.DelayClientCreationUntilIsNeeded = AnsiConsole.Confirm("Do you want to [green]'Delay'[/] the client creation until is needed?");
                    continue;
                }

                if (!_validator.Validate(nameof(Dto.RunInParallel)))
                {
                    _validator.PrintValidationErrors(nameof(Dto.RunInParallel));
                    _roundDto.RunInParallel = AnsiConsole.Confirm("Do you want to run all your http iterations in [green]'Parallel'[/]?");
                    continue;
                }

                HttpIterationDto iterationCommand = new HttpIterationDto();
                IterationValidator validator = new IterationValidator(iterationCommand);
                IterationChallengeUserService iterationUserService = new(SkipOptionalFields, iterationCommand, validator);
                iterationUserService.Challenge();

                Dto.Iterations.Add(iterationCommand);

                AnsiConsole.MarkupLine("[bold]Type [blue]add[/] to add a new http iteration or press [blue]enter[/] [/]");

                string? action = Console.ReadLine()?.Trim().ToLower();
                if (action == "add")
                {
                    continue;
                }
                break;
            }

            _roundDto.IsValid = true;
        }

        public void ResetOptionalFields()
        {
            if (!_skipOptionalFields)
            {
                _roundDto.StartupDelay = -1;
                _roundDto.DelayClientCreationUntilIsNeeded = null;
                _roundDto.RunInParallel = null;
            }
        }
    }
}
