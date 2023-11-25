using LPS.Domain;
using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Net;
using FluentValidation;
using FluentValidation.Results;
using LPS.UI.Common;
using System.Text;

namespace LPS.UI.Core.UI.Build.Services
{
    internal class LPSRequestProfileValidator : LPSBaseValidator<LPSHttpRequestProfile.SetupCommand, LPSHttpRequestProfile>
    {

        LPSHttpRequestProfile.SetupCommand _command;
        ValidationResult _validationResults;
        private string[] _httpMethods = { "GET", "HEAD", "POST", "PUT", "PATCH", "DELETE", "CONNECT", "OPTIONS", "TRACE" };
        public LPSRequestProfileValidator(LPSHttpRequestProfile.SetupCommand command)
        {
            _command = command;
            RuleFor(command => command.Httpversion).Must(version => version == "1.0" || version == "1.1")
                .WithMessage("Invalid Http version. Supported versions are 1.0 and 1.1");
            RuleFor(command => command.HttpMethod).Must(httpMethod => _httpMethods.Any(method => method == httpMethod))
                .WithMessage("Invalid Http Method");
            RuleFor(command => command.URL).Must(url =>
            {
                Uri result;
                return Uri.TryCreate(url, UriKind.Absolute, out result)
                && (result.Scheme == Uri.UriSchemeHttp || result.Scheme == Uri.UriSchemeHttps);
            }).WithMessage("Invalid URL");
            RuleFor(command => command.DownloadHtmlEmbeddedResources)
                .NotNull()
                .WithMessage("DownloadHtmlEmbeddedResources property can't be null");
            RuleFor(command => command.SaveResponse)
                .NotNull()
                .WithMessage("SaveResponse property can't be null");

        }

        public override LPSHttpRequestProfile.SetupCommand Command { get { return _command; } }
    }
}
