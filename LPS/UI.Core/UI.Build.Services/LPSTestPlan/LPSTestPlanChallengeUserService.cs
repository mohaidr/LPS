using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;
using System.Text.RegularExpressions;
using LPS.Domain;
using LPS.UI.Common;
using LPS.UI.Core.LPSValidators;

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
            Console.ForegroundColor= ConsoleColor.Cyan;
                Console.WriteLine("=================== Create Your Test Plan ===================");
            Console.ResetColor();
            while (true)
            {
                if (!_validator.Validate(nameof(Command.Name)))
                {
                    Console.WriteLine("Test name should be between 1 and 20 characters and does not accept special characters");
                    _command.Name = ChallengeService.Challenge("-testName");
                    continue;
                }

                if (!_validator.Validate(nameof(Command.NumberOfClients)))
                {
                    Console.WriteLine("The number of clients to execute your runs. " +
                        "The number should be a valid positive number greater than 0");

                    int numberOfClients;
                    if (int.TryParse(ChallengeService.Challenge("-numberOfClients"), out numberOfClients))
                    {
                        _command.NumberOfClients = numberOfClients;
                    }

                    continue;
                }

                if (!_validator.Validate(nameof(Command.RampUpPeriod)))
                {
                    Console.WriteLine("The time to wait until a new client connects to your site");

                    int rampupPeriod;
                    if (int.TryParse(ChallengeService.Challenge("-rampupPeriod"), out rampupPeriod))
                    {
                        _command.RampUpPeriod = rampupPeriod;
                    }

                    continue;
                }

                if (!_validator.Validate(nameof(Command.DelayClientCreationUntilIsNeeded)))
                {
                    Console.WriteLine("Delay the client creation until the client is needed");

                    switch (ChallengeService.Challenge("-delayClientCreationUntilNeeded").ToUpper())
                    {
                        case "Y":
                            _command.DelayClientCreationUntilIsNeeded = true; break;
                        case "N":
                            _command.DelayClientCreationUntilIsNeeded = false; break;
                    }
                    continue;
                }

                if (!_validator.Validate(nameof(Command.RunInParallel)))
                {
                    Console.WriteLine("Would you like to execute your runs in parallel");

                    switch (ChallengeService.Challenge("-runInParallel").ToUpper())
                    {
                        case "Y":
                            _command.RunInParallel = true; break;
                        case "N":
                            _command.RunInParallel = false; break;
                    }

                    continue;
                }

                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine("=================== Add Http Run ===================");
                Console.ResetColor();
                var lpsRunCommand = new LPSHttpRun.SetupCommand();
                LPSRunValidator validator = new LPSRunValidator(lpsRunCommand);
                LPSRunChallengeUserService lpsRunUserService = new LPSRunChallengeUserService(SkipOptionalFields, lpsRunCommand, validator);
                lpsRunUserService.Challenge();

                Command.LPSHttpRuns.Add(lpsRunCommand);

                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine("=================== Http Run Has Been Added ===================");
                Console.ResetColor();

                Console.WriteLine("Enter \"add\" to add a new http run to your test plan");

                string action = Console.ReadLine().Trim().ToLower();
                if (action == "add")
                {
                    continue;
                }
                break;
            }

            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("=================== Plan Has Been Created ===================");
            Console.ResetColor();
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
