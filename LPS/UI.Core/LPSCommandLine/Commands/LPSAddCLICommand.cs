using FluentValidation;
using FluentValidation.Results;
using LPS.Domain;
using LPS.UI.Common;
using LPS.UI.Common.Extensions;
using LPS.UI.Common.Helpers;
using LPS.UI.Core.LPSCommandLine.Bindings;
using LPS.UI.Core.LPSValidators;
using LPS.UI.Core.UI.Build.Services;
using System;
using System.Collections.Generic;
using System.CommandLine;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace LPS.UI.Core.LPSCommandLine.Commands
{
    internal class LPSAddCLICommand: ILPSCLICommand
    {
        private Command _rootLpsCliCommand;
        private LPSTestPlan.SetupCommand _planSetupCommand;
        private Command _addCommand;
        private string[] _args;
        internal LPSAddCLICommand(Command rootLpsCliCommand, LPSTestPlan.SetupCommand planSetupCommand, string[] args)
        {
            _rootLpsCliCommand = rootLpsCliCommand;
            _planSetupCommand = planSetupCommand;
            _args = args;
            Setup();
        }

        private void Setup()
        {
            _addCommand = new Command("add", "Add an http request");

            LPSCommandLineOptions.AddOptionsToCommand(_addCommand, typeof(LPSCommandLineOptions.LPSAddCommandOptions));
            _rootLpsCliCommand.AddCommand(_addCommand);
        }

        public void Execute(CancellationToken cancellationToken)
        {
            _addCommand.SetHandler((testName, lpsTestCase) =>
            {
                bool isValidPlan, isValidTestCase, isValidRequestProfile;
                ValidationResult results;
                _planSetupCommand = LPSSerializationHelper.Deserialize<LPSTestPlan.SetupCommand>(File.ReadAllText($"{testName}.json"));
                var planValidator = new LPSTestPlanValidator(_planSetupCommand);
                results = planValidator.Validate();
                isValidPlan = results.IsValid;
                if (!isValidPlan)
                {
                    results.PrintValidationErrors();
                }
                var lpsTestCaseValidator = new LPSTestCaseValidator(lpsTestCase);
                results = lpsTestCaseValidator.Validate();
                isValidTestCase = results.IsValid;
                if (!isValidTestCase)
                {
                    results.PrintValidationErrors();
                }
                var lpsRequestProfileValidator = new LPSRequestProfileValidator(lpsTestCase.LPSRequestProfile);
                results = lpsRequestProfileValidator.Validate();
                isValidRequestProfile = results.IsValid;
                if (!isValidRequestProfile)
                {
                    results.PrintValidationErrors();
                }

                if (isValidTestCase && isValidPlan && isValidRequestProfile)
                {
                    _planSetupCommand.LPSTestCases.Add(lpsTestCase);
                    _planSetupCommand.IsValid = true;
                    string json = LPSSerializationHelper.Serialize(_planSetupCommand);
                    File.WriteAllText($"{testName}.json", json);
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine("Your test case has been added successfully");
                    Console.ResetColor();
                }
            },
            LPSCommandLineOptions.LPSAddCommandOptions.TestNameOption,
            new LPSTestCaseCommandBinder());
            _rootLpsCliCommand.Invoke(_args);
        }
    }
}
