using FluentValidation.Results;
using LPS.Domain;
using LPS.Infrastructure.Common;
using LPS.UI.Common;
using LPS.UI.Common.Extensions;
using LPS.UI.Core.LPSCommandLine.Bindings;
using LPS.UI.Core.LPSValidators;
using LPS.UI.Core.UI.Build.Services;
using Spectre.Console;
using System;
using System.Collections.Generic;
using System.CommandLine;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ValidationResult = FluentValidation.Results.ValidationResult;

namespace LPS.UI.Core.LPSCommandLine.Commands
{
    internal class CreateCLICommand: ICLICommand
    {
        private Command _rootLpsCliCommand;
        private TestPlan.SetupCommand _planSetupCommand;
        private string[] _args;
        private Command _createCommand;
        internal CreateCLICommand(Command rootLpsCliCommand, TestPlan.SetupCommand planSetupCommand, string[] args)
        {
            _rootLpsCliCommand = rootLpsCliCommand;
            _planSetupCommand = planSetupCommand;
            _args = args;
            Setup();
        }

        private void Setup()
        {
            _createCommand = new Command("create", "Create a new test");
            CommandLineOptions.AddOptionsToCommand(_createCommand, typeof(CommandLineOptions.LPSCreateCommandOptions));
            _rootLpsCliCommand.AddCommand(_createCommand);
        }

        public void Execute(CancellationToken cancellationToken)
        {
            _createCommand.SetHandler((lpaTestPlan) =>
            {
                bool isValidPlan;
                ValidationResult results;
                var planValidator = new TestPlanValidator(lpaTestPlan);
                results = planValidator.Validate();
                isValidPlan = results.IsValid;
                if (!isValidPlan)
                {
                    results.PrintValidationErrors();
                }
                else
                {
                    _planSetupCommand.Name = lpaTestPlan.Name;
                    _planSetupCommand.NumberOfClients = lpaTestPlan.NumberOfClients;
                    _planSetupCommand.DelayClientCreationUntilIsNeeded = lpaTestPlan.DelayClientCreationUntilIsNeeded;
                    _planSetupCommand.RampUpPeriod = lpaTestPlan.RampUpPeriod;
                    _planSetupCommand.RunInParallel = lpaTestPlan.RunInParallel;
                    string json = SerializationHelper.Serialize(_planSetupCommand);
                    File.WriteAllText($"{lpaTestPlan.Name}.json", json);
                    AnsiConsole.MarkupLine("[Green]Your test plan has been created successfully[/]");
                }
            },
            new TestPlanCommandBinder());
            _rootLpsCliCommand.Invoke(_args);
        }
    }
}
