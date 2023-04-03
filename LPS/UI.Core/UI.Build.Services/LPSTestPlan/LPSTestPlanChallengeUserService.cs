using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;
using System.Text.RegularExpressions;
using LPS.Domain;
using LPS.UI.Common;

namespace LPS.UI.Core.UI.Build.Services
{
    internal class LPSTestPlanChallengeUserService : IChallengeUserService<LPSTestPlan.SetupCommand, LPSTestPlan>
    {
        IValidator<LPSTestPlan.SetupCommand, LPSTestPlan> _validator;
        LPSTestPlan.SetupCommand _command;
        public LPSTestPlan.SetupCommand Command { get { return _command; } set { value = _command; } }
        public bool SkipOptionalFields { get { return _skipOptionalFields; } set { value = _skipOptionalFields; } }

        private bool _skipOptionalFields;
        public LPSTestPlanChallengeUserService(bool skipOptionalFields, LPSTestPlan.SetupCommand command, IValidator<LPSTestPlan.SetupCommand, LPSTestPlan> validator)
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
            while (true)
            {
                if (!_validator.Validate("-testname"))
                {
                    Console.WriteLine("Test name should at least be of 2 charachters and can only contains letters, numbers, ., _ and -");
                    _command.Name = ChallengeService.Challenge("-testName");
                    continue;
                }

                var lpsTestCaseCommand = new LPSTestCase.SetupCommand();
                LPSTestCaseValidator validator= new LPSTestCaseValidator(lpsTestCaseCommand);
                LPSTestCaseChallengeUserService lpsTestCaseUserService = new LPSTestCaseChallengeUserService(SkipOptionalFields, lpsTestCaseCommand,validator);
                lpsTestCaseUserService.Challenge();

                Command.LPSTestCases.Add(lpsTestCaseCommand);

                Console.WriteLine("Enter \"add\" to add new test case to your test plan");

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
                _command.Name = string.Empty;
            }
        }
    }
}
