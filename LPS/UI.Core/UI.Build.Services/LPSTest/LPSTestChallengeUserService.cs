using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;
using System.Text.RegularExpressions;
using LPS.Domain;
using LPS.UI.Common;

namespace LPS.UI.Core.UI.Build.Services
{
    internal class LPSTestChallengeUserService : IChallengeUserService<LPSTest.SetupCommand, LPSTest>
    {
        IValidator<LPSTest.SetupCommand, LPSTest> _validator;
        LPSTest.SetupCommand _command;
        public LPSTest.SetupCommand Command { get { return _command; } set { value = _command; } }
        public bool SkipOptionalFields { get { return _skipOptionalFields; } set { value = _skipOptionalFields; } }

        private bool _skipOptionalFields;
        public LPSTestChallengeUserService(bool skipOptionalFields, LPSTest.SetupCommand command, IValidator<LPSTest.SetupCommand, LPSTest> validator)
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

                var lpsRequestWrapperCommand = new LPSRequestWrapper.SetupCommand();
                LPSRequestWrapperValidator validator= new LPSRequestWrapperValidator(lpsRequestWrapperCommand);
                LPSRequestWrapperChallengeUserService lpsRequestWrapperUserService = new LPSRequestWrapperChallengeUserService(SkipOptionalFields, lpsRequestWrapperCommand,validator);
                lpsRequestWrapperUserService.Challenge();

                Command.LPSRequestWrappers.Add(lpsRequestWrapperCommand);

                Console.WriteLine("Enter \"add\" to add new http request to the collection or click enter to start your test");

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
