using LPS.Domain;
using LPS.UI.Common;
using System;
using System.Collections.Generic;
using System.Text;

namespace LPS.UI.Core.UI.Build.Services
{
    internal class LPSTestCaseChallengeUserService : IChallengeUserService<LPSTestCase.SetupCommand, LPSTestCase>
    {
        IValidator<LPSTestCase.SetupCommand, LPSTestCase> _validator;

        public LPSTestCaseChallengeUserService(bool skipOptionalFields, LPSTestCase.SetupCommand command, IValidator<LPSTestCase.SetupCommand, LPSTestCase> validator)
        {
            _skipOptionalFields = skipOptionalFields;
            _command = command;
            _validator = validator;
        }
        public bool SkipOptionalFields { get { return _skipOptionalFields; } set { value = _skipOptionalFields; } }
        private bool _skipOptionalFields;

        LPSTestCase.SetupCommand _command;
        public LPSTestCase.SetupCommand Command { get { return _command; } set { value = _command; } }
        public void Challenge()
        {
            if (!_skipOptionalFields)
            {
                ResetOptionalFields();
            }

            while (true)
            {
                if (!_validator.Validate("-requestname"))
                {
                    Console.WriteLine("Give your request a name, it should at least be 2 charachters and can only contains letters, numbers, ., _ and -");
                    _command.Name = ChallengeService.Challenge("-requestname");
                    continue;
                }

                if (!_validator.Validate("-requestCount"))
                {
                    try
                    {
                        Console.WriteLine("Enter the number of requests");
                        _command.RequestCount = int.Parse(ChallengeService.Challenge("-requestCount"));
                        continue;
                    }
                    catch
                    {
                        continue;
                    }
                }
                break;
            }

            LPSRequestValidator validator = new LPSRequestValidator(_command.LPSRequest);
            LPSRequestChallengeUserService lpsRequestUserService = new LPSRequestChallengeUserService(SkipOptionalFields, _command.LPSRequest, validator);
            lpsRequestUserService.Challenge();
        }

        public void ResetOptionalFields()
        {
            if (!_skipOptionalFields)
            {
                //reset optional fields if there are any
            }
        }
    }
}
