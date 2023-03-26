using LPS.Domain;
using LPS.UI.Common;
using System;
using System.Collections.Generic;
using System.Text;

namespace LPS.UI.Core.UI.Build.Services
{
    internal class LPSRequestWrapperChallengeUserService : IChallengeUserService<LPSRequestWrapper.SetupCommand, LPSRequestWrapper>
    {
        IValidator<LPSRequestWrapper.SetupCommand, LPSRequestWrapper> _validator;

        public LPSRequestWrapperChallengeUserService(bool skipOptionalFields, LPSRequestWrapper.SetupCommand command, IValidator<LPSRequestWrapper.SetupCommand, LPSRequestWrapper> validator)
        {
            _skipOptionalFields = skipOptionalFields;
            _command = command;
            _validator = validator;
        }
        public bool SkipOptionalFields { get { return _skipOptionalFields; } set { value = _skipOptionalFields; } }
        private bool _skipOptionalFields;

        LPSRequestWrapper.SetupCommand _command;
        public LPSRequestWrapper.SetupCommand Command { get { return _command; } set { value = _command; } }
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

                if (!_validator.Validate("-repeat"))
                {
                    try
                    {
                        Console.WriteLine("Enter the number of async calls, it should be a valid positive integer number");
                        _command.NumberofAsyncRepeats = int.Parse(ChallengeService.Challenge("-repeat"));
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
                _command.Name = string.Empty;
            }
        }
    }
}
