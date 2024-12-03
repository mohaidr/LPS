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
using Microsoft.AspNetCore.Http;
using LPS.DTOs;
using System.CommandLine;

namespace LPS.UI.Core.LPSValidators
{
    internal class RequestValidator : CommandBaseValidator<HttpRequestDto, Domain.HttpRequest>
    {

        readonly HttpRequestDto _requestDto;
        readonly string[] _httpMethods = { "GET", "HEAD", "POST", "PUT", "PATCH", "DELETE", "CONNECT", "OPTIONS", "TRACE" };
        public RequestValidator(HttpRequestDto requestDto)
        {
            ArgumentNullException.ThrowIfNull(requestDto);
            _requestDto = requestDto;

            RuleFor(dto => dto.HttpVersion)
                .NotEmpty()
                .WithMessage("The 'HttpVersion' cannot be null or empty.")
                .Must(version => string.IsNullOrEmpty(version)
                    || version.StartsWith("$")
                    || version == "1.0"
                    || version == "1.1"
                    || version == "2.0")
                .WithMessage("The accepted 'Http Versions' are (\"1.0\", \"1.1\", \"2.0\") or placeholders starting with '$'")
                .Must((dto, version) =>
                    string.IsNullOrEmpty(version)
                    || !dto.SupportH2C.HasValue
                    || !dto.SupportH2C.Value
                    || version.Equals("2.0"))
                .WithMessage("H2C only works with HTTP/2");

            RuleFor(dto => dto.HttpMethod)
                .NotEmpty()
                .WithMessage("The 'HttpMethod' cannot be null or empty.")
                .Must(httpMethod => string.IsNullOrEmpty(httpMethod)
                    || httpMethod.StartsWith("$")
                    || _httpMethods.Any(method => method.Equals(httpMethod, StringComparison.OrdinalIgnoreCase)))
                .WithMessage("The supported 'Http Methods' are (\"GET\", \"HEAD\", \"POST\", \"PUT\", \"PATCH\", \"DELETE\", \"CONNECT\", \"OPTIONS\", \"TRACE\") or placeholders starting with '$'");

            RuleFor(dto => dto.URL)
                .NotEmpty()
                .WithMessage("The 'URL' cannot be null or empty.")
                .Must(url => string.IsNullOrEmpty(url)
                    || url.StartsWith("$")
                    || (Uri.TryCreate(url, UriKind.Absolute, out Uri result)
                        && (result.Scheme == Uri.UriSchemeHttp || result.Scheme == Uri.UriSchemeHttps)))
                .WithMessage("The 'URL' must be a valid URL according to RFC 3986 or a placeholder starting with '$'")
                .Must((dto, url) =>
                    string.IsNullOrEmpty(url)
                    || url.StartsWith("$")
                    || !dto.SupportH2C.HasValue
                    || !dto.SupportH2C.Value
                    || url.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
                .WithMessage("H2C only works with the HTTP schema or placeholders starting with '$'");

            RuleFor(dto => dto.SupportH2C)
                .NotNull()
                .When(dto =>
                    !string.IsNullOrEmpty(dto.URL)
                    && dto.URL.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                    && !string.IsNullOrEmpty(Dto.HttpVersion)
                    && Dto.HttpVersion.Equals("2.0"))
                .WithMessage("'SupportH2C' must be (y) or (n)");

            RuleFor(dto => dto.SaveResponse)
                .NotNull()
                .WithMessage("'Save Response' must be (y) or (n)");

            RuleFor(dto => dto.DownloadHtmlEmbeddedResources)
                .NotNull()
                .WithMessage("'Download Html Embedded Resources' must be (y) or (n)");


            // Enforce HTTP when SupportH2C is true
            When(dto => dto.SupportH2C == true, () =>
            {
                RuleFor(dto => dto.URL)
                    .Must(url =>
                    {
                        return string.IsNullOrEmpty(url) || url.StartsWith("http://", StringComparison.OrdinalIgnoreCase);
                    })
                    .WithMessage("When 'SupportH2C' is enabled, the 'URL' must use the HTTP scheme.");
                RuleFor(dto => dto.HttpVersion)
                    .Must(httpVersion =>
                    {
                        return string.IsNullOrEmpty(httpVersion) || httpVersion.Equals("2.0");
                    })
                    .WithMessage("When 'SupportH2C' is enabled, the 'Http Version' must be set to 2.0.");
            });
        }

        public override HttpRequestDto Dto { get { return _requestDto; } }
    }
}
