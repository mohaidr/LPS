using System;
using System.Linq;
using System.Net.Http;
using System.Threading;
using FluentValidation;
using LPS.Domain.Common.Interfaces;
using LPS.Domain.Domain.Common.Validation;
using YamlDotNet.Core;

namespace LPS.Domain
{

    public partial class HttpRequest
    {
        public new class Validator : CommandBaseValidator<HttpRequest, SetupCommand>
        {
            ILogger _logger;
            IRuntimeOperationIdProvider _runtimeOperationIdProvider;
            IPlaceholderResolverService _placeHolderServiceResolver;
            IClientService<HttpRequest, HttpResponse> _httpClient;
            HttpRequest _entity;
            HttpRequest.SetupCommand _command;
            private string[] _httpMethods = { "GET", "HEAD", "POST", "PUT", "PATCH", "DELETE", "CONNECT", "OPTIONS", "TRACE" };
            public Validator(HttpRequest entity, HttpRequest.SetupCommand command, bool isExectionMode, IClientService<HttpRequest, HttpResponse> httpClient, IPlaceholderResolverService placeHolderServiceResolver, ILogger logger, IRuntimeOperationIdProvider runtimeOperationIdProvider)
            {
                _logger = logger;
                _runtimeOperationIdProvider = runtimeOperationIdProvider;
                _entity = entity;
                _command = command;
                _httpClient = httpClient;
                _placeHolderServiceResolver = placeHolderServiceResolver;
                #region Validation Rules
                RuleFor(command => command.HttpVersion)
                    .NotEmpty()
                    .WithMessage("The 'HttpVersion' cannot be null or empty.")
                    .Must(version => string.IsNullOrEmpty(version)
                        || (!isExectionMode && _placeHolderServiceResolver.ResolvePlaceholdersAsync<string>(version, _httpClient?.SessionId, CancellationToken.None).Result?.StartsWith("$") == true )
                        || version == "1.0"
                        || version == "1.1"
                        || version == "2.0")
                    .WithMessage("The accepted 'Http Versions' are (\"1.0\", \"1.1\", \"2.0\") or placeholders starting with '$'")
                    .Must((command, version) =>
                        string.IsNullOrEmpty(version)
                        || !command.SupportH2C.HasValue
                        || !command.SupportH2C.Value
                        || version.Equals("2.0"))
                    .WithMessage("H2C only works with HTTP/2");

                RuleFor(command => command.HttpMethod)
                    .NotEmpty()
                    .WithMessage("The 'HttpMethod' cannot be null or empty.")
                    .Must(httpMethod => string.IsNullOrEmpty(httpMethod)
                        || (_placeHolderServiceResolver.ResolvePlaceholdersAsync<string>(httpMethod, _httpClient?.SessionId, CancellationToken.None).Result?.StartsWith("$") == true && !isExectionMode)
                        || _httpMethods.Any(method => method.Equals(httpMethod, StringComparison.OrdinalIgnoreCase)))
                    .WithMessage("The supported 'Http Methods' are (\"GET\", \"HEAD\", \"POST\", \"PUT\", \"PATCH\", \"DELETE\", \"CONNECT\", \"OPTIONS\", \"TRACE\") or placeholders starting with '$'");

                RuleFor(command => command.URL)
                    .NotEmpty()
                    .WithMessage("The 'URL' cannot be null or empty.")
                    .Must(url => string.IsNullOrEmpty(url)
                        || (_placeHolderServiceResolver.ResolvePlaceholdersAsync<string>(url, _httpClient?.SessionId, CancellationToken.None).Result?.StartsWith("$") == true && !isExectionMode)
                        || (Uri.TryCreate(url, UriKind.Absolute, out Uri result)
                            && (result.Scheme == Uri.UriSchemeHttp || result.Scheme == Uri.UriSchemeHttps)))
                    .WithMessage("The 'URL' must be a valid URL according to RFC 3986 or a placeholder starting with '$'")
                    .Must((command, url) =>
                        string.IsNullOrEmpty(url)
                        || !command.SupportH2C.HasValue
                        || !command.SupportH2C.Value
                        || url.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
                    .WithMessage("H2C only works with the HTTP schema or placeholders starting with '$'");

                RuleFor(command => command.SupportH2C)
                    .NotNull()
                    .When(command =>
                        !string.IsNullOrEmpty(command.URL)
                        && command.URL.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                        && !string.IsNullOrEmpty(command.HttpVersion)
                        && command.HttpVersion.Equals("2.0"))
                    .WithMessage("'SupportH2C' must be true or false");

                RuleFor(command => command.SaveResponse)
                    .NotNull()
                    .WithMessage("'SupportH2C' must be true or false");
                
                RuleFor(command => command.DownloadHtmlEmbeddedResources)
                    .NotNull()
                    .WithMessage("'Download Html Embedded Resources' must be true or false");

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

                _command.IsValid = base.Validate();
            }

            public override SetupCommand Command => _command;
            public override HttpRequest Entity => _entity;
        }
    }
}
