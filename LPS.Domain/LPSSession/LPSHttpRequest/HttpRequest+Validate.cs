using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using LPS.Domain.Common.Interfaces;
using LPS.Domain.Domain.Common.Validation;

namespace LPS.Domain
{

    public partial class HttpRequest
    {
        public new class Validator : CommandBaseValidator<HttpRequest, HttpRequest.SetupCommand>
        {
            ILogger _logger;
            IRuntimeOperationIdProvider _runtimeOperationIdProvider;
            HttpRequest _entity;
            HttpRequest.SetupCommand _command;
            private string[] _httpMethods = { "GET", "HEAD", "POST", "PUT", "PATCH", "DELETE", "CONNECT", "OPTIONS", "TRACE" };
            public Validator(HttpRequest entity, HttpRequest.SetupCommand command, ILogger logger, IRuntimeOperationIdProvider runtimeOperationIdProvider)
            {
                _logger = logger;
                _runtimeOperationIdProvider = runtimeOperationIdProvider;
                _entity = entity;
                _command = command;

                #region Validation Rules
                RuleFor(command => command.HttpVersion)
                    .Must(version => version == "1.0" || version == "1.1" || version == "2.0")
                    .WithMessage("The accepted 'Http Versions' are (\"1.0\", \"1.1\", \"2.0\")")
                    .Must((command, version) => // either h2c is not enabled or the version must be http/2
                        string.IsNullOrEmpty(version)
                        || !command.SupportH2C.HasValue
                        || !command.SupportH2C.Value
                        || version.Equals("2.0"))
                .WithMessage("H2C only works with the HTTP/2");

                RuleFor(command => command.HttpMethod)
                    .Must(httpMethod => _httpMethods.Any(method => method.Equals(httpMethod, StringComparison.OrdinalIgnoreCase)))
                    .WithMessage("The supported 'Http Methods' are (\"GET\", \"HEAD\", \"POST\", \"PUT\", \"PATCH\", \"DELETE\", \"CONNECT\", \"OPTIONS\", \"TRACE\") ");
                
                RuleFor(command => command.URL)
                    .Must(url =>
                    {
                        return Uri.TryCreate(url, UriKind.Absolute, out Uri result)
                        && (result.Scheme == Uri.UriSchemeHttp || result.Scheme == Uri.UriSchemeHttps);
                    })
                    .WithMessage("The 'URL' must be a valid URL according to RFC 3986")
                    .Must((command, url) => // either h2c is not enabled or the url must start with http
                        string.IsNullOrEmpty(url)
                        || !command.SupportH2C.HasValue
                        || !command.SupportH2C.Value
                        || url.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
                    .WithMessage("H2C only works with the HTTP schema");
                
                RuleFor(command => command.SaveResponse)
                    .NotNull()
                    .WithMessage("'SupportH2C' must be (y) or (n)");
                
                RuleFor(command => command.DownloadHtmlEmbeddedResources)
                    .NotNull()
                    .WithMessage("'Download Html Embedded Resources' must be (y) or (n)");
                
                RuleFor(command => command.SupportH2C)
                .NotNull()
                .When(command =>
                    !string.IsNullOrEmpty(command.URL)
                    && command.URL.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                    && !string.IsNullOrEmpty(Command.HttpVersion)
                    && Command.HttpVersion.Equals("2.0"))
                .WithMessage("'SupportH2C' must be (y) or (n)");

                // Enforce HTTP when SupportH2C is true
                When(command => command.SupportH2C == true, () =>
                {
                    RuleFor(command => command.URL)
                        .Must(url =>
                        {
                            return string.IsNullOrEmpty(url) || url.StartsWith("http://", StringComparison.OrdinalIgnoreCase);
                        })
                        .WithMessage("When 'SupportH2C' is enabled, the 'URL' must use the HTTP scheme.");
                    RuleFor(command => command.HttpVersion)
                        .Must(httpVersion =>
                        {
                            return string.IsNullOrEmpty(httpVersion) || httpVersion.Equals("2.0");
                        })
                        .WithMessage("When 'SupportH2C' is enabled, the 'Http Version' must be set to 2.0.");
                });

                //TODO: Validate http headers
                #endregion

                if (entity.Id != default && command.Id.HasValue && entity.Id != command.Id)
                {
                    _logger.Log(_runtimeOperationIdProvider.OperationId, "LPS Request Profile: Entity Id Can't be Changed, The Id value will be ignored", LPSLoggingLevel.Warning);
                }

            }

            public override SetupCommand Command => _command;
            public override HttpRequest Entity => _entity;
        }
    }
}
