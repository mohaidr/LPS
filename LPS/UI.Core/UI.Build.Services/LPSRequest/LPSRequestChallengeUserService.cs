using LPS.Domain;
using LPS.UI.Common;
using System;


namespace LPS.UI.Core.UI.Build.Services
{
    internal class LPSRequestChallengeUserService : IChallengeUserService<LPSRequest.SetupCommand, LPSRequest>
    {
        IValidator<LPSRequest.SetupCommand, LPSRequest> _validator;
        public LPSRequestChallengeUserService(bool skipOptionalFields, LPSRequest.SetupCommand command, IValidator<LPSRequest.SetupCommand, LPSRequest> validator)
        {
            _skipOptionalFields = skipOptionalFields;
            _command = command;
            _validator = validator;
        }
        public bool SkipOptionalFields { get { return _skipOptionalFields; } set { value = _skipOptionalFields; } }
        private bool _skipOptionalFields;

        LPSRequest.SetupCommand _command;
        public LPSRequest.SetupCommand Command { get { return _command; } set { value = _command; } }
        public void Challenge()
        {
            if (!_skipOptionalFields)
            {
                ResetOptionalFields();
            }

            while (true)
            {
                if (!_validator.Validate("-httpmethod"))
                {
                    Console.WriteLine("Enter a Valid Http Method");
                    _command.HttpMethod = ChallengeService.Challenge("-httpmethod");
                    continue;
                }

                if (!_validator.Validate("-url"))
                {
                    Console.WriteLine("Enter a valid URL e.g (http(s)://example.com)");
                    _command.URL = ChallengeService.Challenge("-url");
                    continue;
                }


                if (!_validator.Validate("-timeout"))
                {
                    try
                    {
                        Console.WriteLine("Enter the HTTP request timeout value in minutes");
                        _command.TimeOut = int.Parse(ChallengeService.Challenge("-timeout"));
                        continue;
                    }
                    catch { }
                }
                if (!_validator.Validate("-httpversion"))
                {
                    Console.WriteLine("Enter a valid http version, currently we only supports 1.0 and 1.1");
                    _command.Httpversion = ChallengeService.Challenge("-httpversion");
                    continue;
                }

                break;
            }


            Console.WriteLine("Enter the request headers");
            Console.WriteLine("Type your header in the following format (headerName: headerValue) and click enter. After you finish entering all headers, enter done");
            _command.HttpHeaders = InputHeaderService.Challenge();


            if (_command.HttpMethod.ToUpper() == "PUT" || _command.HttpMethod.ToUpper() == "POST" || _command.HttpMethod.ToUpper() == "PATCH")
            {
                Console.WriteLine("Add payload to the request, enter a valid path to read the payload from");
                _command.Payload = InputPayloadService.Challenge();
            }
        }

        public void ResetOptionalFields()
        {
            if (!_skipOptionalFields)
            {
                _command.TimeOut = -1;
                _command.Httpversion = string.Empty;
            }
        }
    }
}
