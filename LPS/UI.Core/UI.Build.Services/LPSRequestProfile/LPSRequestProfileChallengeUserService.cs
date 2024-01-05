using LPS.Domain;
using LPS.UI.Common;
using System;


namespace LPS.UI.Core.UI.Build.Services
{
    internal class LPSRequestProfileChallengeUserService : IChallengeUserService<LPSHttpRequestProfile.SetupCommand, LPSHttpRequestProfile>
    {
        ILPSBaseValidator<LPSHttpRequestProfile.SetupCommand, LPSHttpRequestProfile> _validator;
        public LPSRequestProfileChallengeUserService(bool skipOptionalFields, LPSHttpRequestProfile.SetupCommand command, ILPSBaseValidator<LPSHttpRequestProfile.SetupCommand, LPSHttpRequestProfile> validator)
        {
            _skipOptionalFields = skipOptionalFields;
            _command = command;
            _validator = validator;
        }
        public bool SkipOptionalFields { get { return _skipOptionalFields; } set { value = _skipOptionalFields; } }
        private bool _skipOptionalFields;

        LPSHttpRequestProfile.SetupCommand _command;
        public LPSHttpRequestProfile.SetupCommand Command { get { return _command; } set { value = _command; } }
        public void Challenge()
        {
            if (!_skipOptionalFields)
            {
                ResetOptionalFields();
            }

            while (true)
            {

                if (!_validator.Validate(nameof(Command.HttpMethod)))
                {
                    Console.WriteLine("Enter a Valid Http Method");
                    _command.HttpMethod = ChallengeService.Challenge("-httpmethod");
                    continue;
                }

                if (!_validator.Validate(nameof(Command.URL)))
                {
                    Console.WriteLine("Enter a valid URL e.g (http(s)://example.com)");
                    _command.URL = ChallengeService.Challenge("-url");
                    continue;
                }
                if (!_validator.Validate(nameof(Command.Httpversion)))
                {
                    Console.WriteLine("Enter a valid http version, currently we only supports 1.0, 1.1 and 2.0");
                    _command.Httpversion = ChallengeService.Challenge("-httpversion");
                    continue;
                }
                if (!_validator.Validate(nameof(Command.DownloadHtmlEmbeddedResources)))
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
                if (!_validator.Validate(nameof(Command.SaveResponse)))
                {
                    Console.WriteLine("Would you like to save the http responses");
                    string input = ChallengeService.Challenge("-saveResponse");
                    switch (input.ToLower())
                    {
                        case "y":
                            Command.SaveResponse = true;
                            break;
                        case "n":
                            Command.SaveResponse = false;
                            break;
                        default:
                            Command.SaveResponse = null;
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
                _command.SaveResponse = null;
            }
        }
    }
}
