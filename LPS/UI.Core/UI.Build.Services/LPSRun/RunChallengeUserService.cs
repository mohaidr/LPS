using LPS.Domain;
using LPS.Domain.Domain.Common.Enums;
using LPS.UI.Common;
using LPS.UI.Core.LPSValidators;
using Spectre.Console;
namespace LPS.UI.Core.UI.Build.Services
{
    internal class RunChallengeUserService : IChallengeUserService<HttpRun.SetupCommand, HttpRun>
    {
        IBaseValidator<HttpRun.SetupCommand, HttpRun> _validator;
        HttpRun.SetupCommand _command;
        private bool _skipOptionalFields;
        public RunChallengeUserService(bool skipOptionalFields, HttpRun.SetupCommand command, IBaseValidator<HttpRun.SetupCommand, HttpRun> validator)
        {
            _skipOptionalFields = skipOptionalFields;
            _command = command;
            _validator = validator;
        }
        public bool SkipOptionalFields { get { return _skipOptionalFields; } set { value = _skipOptionalFields; } }
        public HttpRun.SetupCommand Command { get { return _command; } set { value = _command; } }
        public void Challenge()
        {
            if (!_skipOptionalFields)
            {
                ResetOptionalFields();
            }
            AnsiConsole.MarkupLine("[underline bold blue]Add 'HTTP RUN' to your plan:[/]");
            while (true)
            {
                if (!_validator.Validate(nameof(Command.Name)))
                {
                    _validator.PrintValidationErrors(nameof(Command.Name));

                    _command.Name = AnsiConsole.Ask<string>("What is the [green]'Name'[/] of your [green]'HTTP RUN'[/]?");
                    continue;
                }

                if (!_validator.Validate(nameof(Command.Mode)))
                {

                    AnsiConsole.MarkupLine("[blue]D[/] stands for duration. [blue]C[/] stands for Cool Down, [blue]R[/] stands for Request Count, [blue]B[/] stands for Batch Size");
                    _validator.PrintValidationErrors(nameof(Command.Mode));
                    _command.Mode = AnsiConsole.Ask<IterationMode>("At which [green]'Mode'[/] the 'HTTP RUN' should be executed?"); ;
                    continue;
                }


                if (!_validator.Validate(nameof(Command.Duration)))
                {

                    _validator.PrintValidationErrors(nameof(Command.Duration));
                    _command.Duration = AnsiConsole.Ask<int>("What is the [green]'Duration' (in seconds)[/] for which each client can send requests to your endpoint?");
                    continue;
                }


                if (!_validator.Validate(nameof(Command.RequestCount)))
                {
                    _validator.PrintValidationErrors(nameof(Command.RequestCount));
                    _command.RequestCount = AnsiConsole.Ask<int>("How [green]'Many Requests'[/] the client is supposed to send?");
                    continue;
                }

                if (!_validator.Validate(nameof(Command.BatchSize)))
                {
                    _validator.PrintValidationErrors(nameof(Command.BatchSize));
                    _command.BatchSize = AnsiConsole.Ask<int>("How [green]'Many Requests'[/] the client has to send in a [green]'Batch'[/]?");
                    continue;
                }
                if (!_validator.Validate(nameof(Command.CoolDownTime)))
                {
                    _validator.PrintValidationErrors(nameof(Command.CoolDownTime));
                    _command.CoolDownTime = AnsiConsole.Ask<int>("For how [green]'Long' (in Milliseconds)[/] the client should pause before running the next batch?");
                    continue;
                }

                if (!_validator.Validate(nameof(Command.MaximizeThroughput)))
                {
                    _validator.PrintValidationErrors(nameof(Command.MaximizeThroughput));
                    _command.MaximizeThroughput = AnsiConsole.Confirm("Do you want to maximize the [green]throughput[/]? Maximizing the throughput will result in [yellow]higher CPU and Memory usage[/]!", false);
                    continue;
                }

                break;
            }
            RequestProfileValidator validator = new RequestProfileValidator(_command.LPSRequestProfile);
            RequestProfileChallengeUserService lpsRequestProfileUserService = new RequestProfileChallengeUserService(SkipOptionalFields, _command.LPSRequestProfile, validator);
            lpsRequestProfileUserService.Challenge();
        }

        public void ResetOptionalFields()
        {
            if (!_skipOptionalFields)
            {
                _command.MaximizeThroughput = null;
            }
        }
    }
}
