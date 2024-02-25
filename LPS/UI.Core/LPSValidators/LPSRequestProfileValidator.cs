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

namespace LPS.UI.Core.LPSValidators
{
    internal class LPSRequestProfileValidator : LPSCommandBaseValidator<LPSHttpRequestProfile.SetupCommand, LPSHttpRequestProfile>
    {

        LPSHttpRequestProfile.SetupCommand _command;
        private string[] _httpMethods = { "GET", "HEAD", "POST", "PUT", "PATCH", "DELETE", "CONNECT", "OPTIONS", "TRACE" };
        public LPSRequestProfileValidator(LPSHttpRequestProfile.SetupCommand command)
        {
            _command = command;


            RuleFor(command => command.Httpversion).Must(version => version == "1.0" || version == "1.1"|| version == "2.0")
                .WithMessage("The accepted 'Http Versions' are (\"1.0\", \"1.1\", \"2.0\")");
            RuleFor(command => command.HttpMethod)
                .Must(httpMethod => _httpMethods.Any(method => method.Equals(httpMethod, StringComparison.OrdinalIgnoreCase)))
                .WithMessage("The supported 'Http Methods' are (\"GET\", \"HEAD\", \"POST\", \"PUT\", \"PATCH\", \"DELETE\", \"CONNECT\", \"OPTIONS\", \"TRACE\") ");
            RuleFor(command => command.URL).Must(url =>
            {
                Uri result;
                return Uri.TryCreate(url, UriKind.Absolute, out result)
                && (result.Scheme == Uri.UriSchemeHttp || result.Scheme == Uri.UriSchemeHttps);
            }).WithMessage("The 'URL' must be a valid URL according to RFC 3986");
            RuleFor(command => command.DownloadHtmlEmbeddedResources)
                .NotNull()
                .WithMessage("'Download Html Embedded Resources' must be (y) or (n)");
            RuleFor(command => command.SaveResponse)
                .NotNull()
                .WithMessage("'Save Response' must be (y) or (n)");

        }

        public override LPSHttpRequestProfile.SetupCommand Command { get { return _command; } }
    }
}
