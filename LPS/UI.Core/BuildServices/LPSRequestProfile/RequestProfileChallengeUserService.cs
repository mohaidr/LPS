using LPS.Domain;
using LPS.UI.Common;
using Spectre.Console;
using System;


namespace LPS.UI.Core.Build.Services
{
    internal class RequestProfileChallengeUserService : IChallengeUserService<HttpRequestProfile.SetupCommand, HttpRequestProfile>
    {
        IBaseValidator<HttpRequestProfile.SetupCommand, HttpRequestProfile> _validator;
        public RequestProfileChallengeUserService(bool skipOptionalFields, HttpRequestProfile.SetupCommand command, IBaseValidator<HttpRequestProfile.SetupCommand, HttpRequestProfile> validator)
        {
            _skipOptionalFields = skipOptionalFields;
            _command = command;
            _validator = validator;
        }
        public bool SkipOptionalFields => _skipOptionalFields;
        private readonly bool _skipOptionalFields;

        HttpRequestProfile.SetupCommand _command;
        public HttpRequestProfile.SetupCommand Command { get { return _command; } set { value = _command; } }
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
                    _command.HttpMethod = AnsiConsole.Ask<string>("What is the [green]'Http Request Method'[/]?");
                    continue;
                }

                if (!_validator.Validate(nameof(Command.URL)))
                {
                    _validator.PrintValidationErrors(nameof(Command.URL));
                    _command.URL = AnsiConsole.Ask<string>("What is the [green]'Http Request URL'[/]?");
                    continue;
                }
                if (!_validator.Validate(nameof(Command.HttpVersion)))
                {
                    _validator.PrintValidationErrors(nameof(_command.HttpVersion));
                    _command.HttpVersion = AnsiConsole.Ask<string>("Which [green]'Http Version'[/] to use?"); ;
                    continue;
                }
                if (!_validator.Validate(nameof(Command.SupportH2C)))
                {
                    _validator.PrintValidationErrors(nameof(Command.SupportH2C));
                    Command.SupportH2C = AnsiConsole.Confirm("Would you like to [green]'Perform'[/] Http2 over cleartext?", false);
                    continue;
                }
                if (!_validator.Validate(nameof(Command.SaveResponse)))
                {
                    _validator.PrintValidationErrors(nameof(Command.SaveResponse));
                    Command.SaveResponse = AnsiConsole.Confirm("Would you like to [green]'Save'[/] the http responses?", false);
                    continue;
                }

                if (!_validator.Validate(nameof(Command.DownloadHtmlEmbeddedResources)))
                {
                    _validator.PrintValidationErrors(nameof(Command.DownloadHtmlEmbeddedResources));
                    Command.DownloadHtmlEmbeddedResources = AnsiConsole.Confirm("If the server returns text/html, would you like to [green]'Download'[/] the html embedded resources?", false);
                    continue;
                }

                break;
            }

            AnsiConsole.MarkupLine("Add request headers as [blue](HeaderName: HeaderValue)[/] each on a line. When finished, type C and press enter");
            _command.HttpHeaders = InputHeaderService.Challenge();


            if (_command.HttpMethod.Equals("PUT", StringComparison.CurrentCultureIgnoreCase) || _command.HttpMethod.Equals("POST", StringComparison.CurrentCultureIgnoreCase) || _command.HttpMethod.Equals("PATCH", StringComparison.CurrentCultureIgnoreCase))
            {
                AnsiConsole.WriteLine("Add payload to your http request.\n - Enter Path:[Path] to read the payload from a path file\n - URL:[URL] to read the payload from a URL \n - Or just add your payload inline");
                _command.Payload = InputPayloadService.Challenge();
            }
        }

        public void ResetOptionalFields()
        {
            if (!_skipOptionalFields)
            {
                _command.HttpVersion = string.Empty;
                _command.DownloadHtmlEmbeddedResources = null;
                _command.SaveResponse = null;
            }
        }
    }
}
