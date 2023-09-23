using LPS.Domain;
using LPS.UI.Common;
using System;


namespace LPS.UI.Core.UI.Build.Services
{
    internal class LPSRequestChallengeUserService : IChallengeUserService<LPSHttpRequest.SetupCommand, LPSHttpRequest>
    {
        IUserValidator<LPSHttpRequest.SetupCommand, LPSHttpRequest> _validator;
        public LPSRequestChallengeUserService(bool skipOptionalFields, LPSHttpRequest.SetupCommand command, IUserValidator<LPSHttpRequest.SetupCommand, LPSHttpRequest> validator)
        {
            _skipOptionalFields = skipOptionalFields;
            _command = command;
            _validator = validator;
        }
        public bool SkipOptionalFields { get { return _skipOptionalFields; } set { value = _skipOptionalFields; } }
        private bool _skipOptionalFields;

        LPSHttpRequest.SetupCommand _command;
        public LPSHttpRequest.SetupCommand Command { get { return _command; } set { value = _command; } }
        public void Challenge()
        {
            if (!_skipOptionalFields)
            {
                ResetOptionalFields();
            }

            while (true)
            {
                if (!_validator.Validate("-httpMethod"))
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
                if (!_validator.Validate("-httpVersion"))
                {
                    Console.WriteLine("Enter a valid http version, currently we only supports 1.0 and 1.1");
                    _command.Httpversion = ChallengeService.Challenge("-httpversion");
                    continue;
                }
                if (!_validator.Validate("-downloadHtmlEmbeddedResources"))
                {
                    Console.WriteLine("If the server returns text/html, would you like to download the html embedded resources");
                    string input = ChallengeService.Challenge("-downloadHtmlEmbeddedResources");
                    switch (input.ToLower())
                    {
                        case "y":
                            Command.DownloadHtmlEmbeddedResources = true;
                            break;
                        case "n":
                            Command.DownloadHtmlEmbeddedResources = false;
                            break;
                        default:
                            Command.DownloadHtmlEmbeddedResources = null;
                            break;
                    }
                    continue;
                }
                break;
            }


            Console.WriteLine("Enter the request headers");
            Console.WriteLine("Type your header in the following format (headerName: headerValue) and click enter. After you finish entering all headers, enter done");
            _command.HttpHeaders = InputHeaderService.Challenge();


            if (_command.HttpMethod.ToUpper() == "PUT" || _command.HttpMethod.ToUpper() == "POST" || _command.HttpMethod.ToUpper() == "PATCH")
            {
                Console.WriteLine("Add payload to the request. Enter Path:[Path] to read the payload from a path file, URL:[URL] to read the payload from a URL or just add your payload inline");
                _command.Payload = InputPayloadService.Challenge();
            }
        }

        public void ResetOptionalFields()
        {
            if (!_skipOptionalFields)
            {
                _command.Httpversion = string.Empty;
                _command.DownloadHtmlEmbeddedResources = null;
            }
        }
    }
}
