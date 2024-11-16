using LPS.Domain;
using LPS.DTOs;
using LPS.UI.Common;
using Spectre.Console;
using System;


namespace LPS.UI.Core.Build.Services
{
    internal class SessionChallengeUserService(bool skipOptionalFields, HttpSessionDto command, string baseUrl, IBaseValidator<HttpSessionDto, HttpSession> validator) : IChallengeUserService<HttpSessionDto, HttpSession>
    {
        readonly IBaseValidator<HttpSessionDto, HttpSession> _validator = validator;
        public bool SkipOptionalFields => _skipOptionalFields;
        private readonly bool _skipOptionalFields = skipOptionalFields;
        private readonly string _baseUrl= baseUrl;
        readonly HttpSessionDto _sessionDto = command;
        public HttpSessionDto Dto => _sessionDto;
        public void Challenge()
        {
            if (!_skipOptionalFields)
            {
                ResetOptionalFields();
            }

            while (true)
            {

                if (!_validator.Validate(nameof(Dto.HttpMethod)))
                {
                    _validator.PrintValidationErrors(nameof(Dto.HttpMethod));
                    _sessionDto.HttpMethod = AnsiConsole.Ask<string>("What is the [green]'Http Request Method'[/]?");
                    continue;
                }

                if (!_validator.Validate(nameof(Dto.URL)))
                {
                    _validator.PrintValidationErrors(nameof(Dto.URL));
                    string inpute= AnsiConsole.Ask<string>("What is the [green]'Http Request URL'[/]?");
                    if (!string.IsNullOrEmpty(_baseUrl) && !inpute.StartsWith("http://") && !inpute.StartsWith("https://"))
                    {
                        inpute = $"{_baseUrl}{inpute}";
                    }
                    _sessionDto.URL = inpute;
                    continue;
                }
                if (!_validator.Validate(nameof(Dto.HttpVersion)))
                {
                    _validator.PrintValidationErrors(nameof(_sessionDto.HttpVersion));
                    _sessionDto.HttpVersion = AnsiConsole.Ask<string>("Which [green]'Http Version'[/] to use?"); ;
                    continue;
                }
                if (!_validator.Validate(nameof(Dto.SupportH2C)))
                {
                    _validator.PrintValidationErrors(nameof(Dto.SupportH2C));
                    Dto.SupportH2C = AnsiConsole.Confirm("Would you like to [green]'Perform'[/] Http2 over cleartext?", false);
                    continue;
                }
                if (!_validator.Validate(nameof(Dto.SaveResponse)))
                {
                    _validator.PrintValidationErrors(nameof(Dto.SaveResponse));
                    Dto.SaveResponse = AnsiConsole.Confirm("Would you like to [green]'Save'[/] the http responses?", false);
                    continue;
                }

                if (!_validator.Validate(nameof(Dto.DownloadHtmlEmbeddedResources)))
                {
                    _validator.PrintValidationErrors(nameof(Dto.DownloadHtmlEmbeddedResources));
                    Dto.DownloadHtmlEmbeddedResources = AnsiConsole.Confirm("If the server returns text/html, would you like to [green]'Download'[/] the html embedded resources?", false);
                    continue;
                }

                break;
            }

            AnsiConsole.MarkupLine("Add request headers as [blue](HeaderName: HeaderValue)[/] each on a line. When finished, type C and press enter");
            _sessionDto.HttpHeaders = InputHeaderService.Challenge();


            if (_sessionDto.HttpMethod.Equals("PUT", StringComparison.CurrentCultureIgnoreCase) || _sessionDto.HttpMethod.Equals("POST", StringComparison.CurrentCultureIgnoreCase) || _sessionDto.HttpMethod.Equals("PATCH", StringComparison.CurrentCultureIgnoreCase))
            {
                AnsiConsole.WriteLine("Add payload to your http request.\n - Enter Path:[Path] to read the payload from a path file\n - URL:[URL] to read the payload from a URL \n - Or just add your payload inline");
                _sessionDto.Payload = InputPayloadService.Challenge();
            }
        }

        public void ResetOptionalFields()
        {
            if (!_skipOptionalFields)
            {
                _sessionDto.HttpVersion = string.Empty;
                _sessionDto.DownloadHtmlEmbeddedResources = null;
                _sessionDto.SaveResponse = null;
            }
        }
    }
}
