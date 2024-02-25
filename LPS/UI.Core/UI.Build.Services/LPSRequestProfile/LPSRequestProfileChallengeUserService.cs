using LPS.Domain;
using LPS.UI.Common;
using Spectre.Console;
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
                    _validator.PrintValidationErrors(nameof(Command.HttpMethod));
                    _command.HttpMethod = AnsiConsole.Ask<string>("What is your [green]Http request method[/]?");
                    continue;
                }

                if (!_validator.Validate(nameof(Command.URL)))
                {
                    _validator.PrintValidationErrors(nameof(Command.URL));
                    _command.URL = AnsiConsole.Ask<string>("What is your [green]Http request URL[/]?");
                    continue;
                }
                if (!_validator.Validate(nameof(Command.Httpversion)))
                {
                    _validator.PrintValidationErrors(nameof(_command.Httpversion));
                                        _command.Httpversion = AnsiConsole.Ask<string>("Which [green]Http version[/] to use?"); ;
                    continue;
                }
                if (!_validator.Validate(nameof(Command.DownloadHtmlEmbeddedResources)))
                {
                    _validator.PrintValidationErrors(nameof(Command.DownloadHtmlEmbeddedResources));
                    Command.DownloadHtmlEmbeddedResources = AnsiConsole.Confirm("If the server returns text/html, would you like to download the html embedded resources?");
                    continue;
                }
                if (!_validator.Validate(nameof(Command.SaveResponse)))
                {
                    _validator.PrintValidationErrors(nameof(Command.SaveResponse));
                    Command.SaveResponse = AnsiConsole.Confirm("Would you like to save the http responses?");
                    continue;
                }
                break;
            }

            AnsiConsole.MarkupLine("Add request headers as [blue](HeaderName: HeaderValue)[/] each on a line. When finished, type C and press enter");
            _command.HttpHeaders = InputHeaderService.Challenge();


            if (_command.HttpMethod.ToUpper() == "PUT" || _command.HttpMethod.ToUpper() == "POST" || _command.HttpMethod.ToUpper() == "PATCH")
            {
                AnsiConsole.WriteLine("Add payload to your http request.\n - Enter Path:[Path] to read the payload from a path file\n - URL:[URL] to read the payload from a URL \n - Or just add your payload inline");
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
