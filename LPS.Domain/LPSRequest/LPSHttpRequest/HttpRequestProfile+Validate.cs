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

    public partial class HttpRequestProfile
    {
        public new class Validator : CommandBaseValidator<HttpRequestProfile, HttpRequestProfile.SetupCommand>
        {
            ILogger _logger;
            IRuntimeOperationIdProvider _runtimeOperationIdProvider;
            HttpRequestProfile _entity;
            HttpRequestProfile.SetupCommand _command;
            private string[] _httpMethods = { "GET", "HEAD", "POST", "PUT", "PATCH", "DELETE", "CONNECT", "OPTIONS", "TRACE" };
            public Validator(HttpRequestProfile entity, HttpRequestProfile.SetupCommand command, ILogger logger, IRuntimeOperationIdProvider runtimeOperationIdProvider)
            {
                _logger = logger;
                _runtimeOperationIdProvider = runtimeOperationIdProvider;
                _entity = entity;
                _command = command;

                #region Validation Rules
                RuleFor(command => command.Httpversion).Must(version => version == "1.0" || version == "1.1" || version == "2.0")
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
                RuleFor(command => command.SaveResponse)
                .NotNull()
                .WithMessage("'Save Response' must be (y) or (n)");
                RuleFor(command => command.DownloadHtmlEmbeddedResources)
                    .NotNull().When(command => command.SaveResponse.HasValue && command.SaveResponse.Value)
                    .WithMessage("'Download Html Embedded Resources' must be (y) or (n)");


                //TODO: Validate http headers

                #endregion

                if (entity.Id != default && command.Id.HasValue && entity.Id != command.Id)
                {
                    _logger.Log(_runtimeOperationIdProvider.OperationId, "LPS Request Profile: Entity Id Can't be Changed, The Id value will be ignored", LPSLoggingLevel.Warning);
                }

                _command.IsValid = base.Validate();
            }

            public override SetupCommand Command => _command;
            public override HttpRequestProfile Entity => _entity;
        }
    }
}
