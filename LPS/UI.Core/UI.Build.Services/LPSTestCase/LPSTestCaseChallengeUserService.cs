using LPS.Domain;
using LPS.UI.Common;
using System;
using System.Collections.Generic;
using System.Text;

namespace LPS.UI.Core.UI.Build.Services
{
    internal class LPSTestCaseChallengeUserService : IChallengeUserService<LPSHttpTestCase.SetupCommand, LPSHttpTestCase>
    {
        ILPSBaseValidator<LPSHttpTestCase.SetupCommand, LPSHttpTestCase> _validator;

        public LPSTestCaseChallengeUserService(bool skipOptionalFields, LPSHttpTestCase.SetupCommand command, ILPSBaseValidator<LPSHttpTestCase.SetupCommand, LPSHttpTestCase> validator)
        {
            _skipOptionalFields = skipOptionalFields;
            _command = command;
            _validator = validator;
        }
        public bool SkipOptionalFields { get { return _skipOptionalFields; } set { value = _skipOptionalFields; } }
        private bool _skipOptionalFields;

        LPSHttpTestCase.SetupCommand _command;
        public LPSHttpTestCase.SetupCommand Command { get { return _command; } set { value = _command; } }
        public void Challenge()
        {
            if (!_skipOptionalFields)
            {
                ResetOptionalFields();
            }
            while (true)
            {
                if (!_validator.Validate(nameof(Command.Name)))
                {
                    Console.WriteLine("Give your test case a name, it should between 1 and 20 charachters");
                    _command.Name = ChallengeService.Challenge("-testCaseName");
                    continue;
                }

                if (!_validator.Validate(nameof(Command.Mode)))
                {
                    Console.WriteLine("Choose Your Iteration Mode, Accepted Values are (DCB,CRB,CB,R,D)");
                    Console.WriteLine("\t- D stands for duration. C stands for Cool Down, R stands for Request Count, B stands for Batch Size ");
                    Console.WriteLine("\t- DCB - Requests will be sent on batches with cooling down between the batches until the duration is elapsed or you press escape");
                    Console.WriteLine("\t- CRB - Requests will be sent on batches with cooling down between the batches until the number of completed requests matches the request count or you press escape");
                    Console.WriteLine("\t- CB - Requests will be sent on batches with cooling down between the batches, the test will not stop until you press escape");
                    Console.WriteLine("\t- R - Test will complete when all the requests are completed or you press escape");
                    Console.WriteLine("\t- D - Test will complete once the duration expires or you press escape");

                    if (Enum.TryParse(ChallengeService.Challenge("-iterationMode"), out LPSHttpTestCase.IterationMode mode))
                    {
                        _command.Mode = mode;
                    }
                    continue;
                }


                if (!_validator.Validate(nameof(Command.Duration)))
                {
                    Console.WriteLine("Enter the duration");
                    int duration;
                    if (int.TryParse(ChallengeService.Challenge("-duration"), out duration))
                    {
                        _command.Duration = duration;
                    }
                    continue;
                }


                if (!_validator.Validate(nameof(Command.RequestCount)))
                {
                    Console.WriteLine("Enter the number of requests");
                    int requestCount;
                    if (int.TryParse(ChallengeService.Challenge("-requestCount"), out requestCount))
                    {
                        _command.RequestCount = requestCount;
                    }
                    continue;
                }


                if (!_validator.Validate(nameof(Command.BatchSize)))
                {
                    Console.WriteLine("Enter the batch size value. Batch size should be less than the request count");
                    int batchSize;
                    if (int.TryParse(ChallengeService.Challenge("-batchSize"), out batchSize))
                    {
                        _command.BatchSize = batchSize;
                    }
                    continue;
                }
                if (!_validator.Validate(nameof(Command.CoolDownTime)))
                {
                    Console.WriteLine("Enter the cool down time. Cool down time should be less than the duration");
                    int coolDownTime;
                    if (int.TryParse(ChallengeService.Challenge("-coolDownTime"), out coolDownTime))
                    {
                        _command.CoolDownTime = coolDownTime;
                    }
                    continue;
                }

                break;
            }

            LPSRequestProfileValidator validator = new LPSRequestProfileValidator(_command.LPSRequestProfile);
            LPSRequestProfileChallengeUserService lpsRequestProfileUserService = new LPSRequestProfileChallengeUserService(SkipOptionalFields, _command.LPSRequestProfile, validator);
            lpsRequestProfileUserService.Challenge();
        }

        public void ResetOptionalFields()
        {
            if (!_skipOptionalFields)
            {
                //reset optional fields if there is any
            }
        }
    }
}
