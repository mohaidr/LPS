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
    internal class RequestValidator : CommandBaseValidator<HttpRequestDto>
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
                .Must(version =>
                {
                    // Allow valid HTTP versions or placeholders
                    return string.IsNullOrEmpty(version)
                        || version.StartsWith("$")
                        || version == "1.0"
                        || version == "1.1"
                        || version == "2.0";
                })
                .WithMessage("The accepted 'Http Versions' are (\"1.0\", \"1.1\", \"2.0\") or placeholders starting with '$'")
                .Must((dto, version) =>
                {
                    // Parse SupportH2C as bool and validate compatibility
                    if (string.IsNullOrWhiteSpace(dto.SupportH2C) || dto.SupportH2C.StartsWith("$"))
                        return true;

                    if (bool.TryParse(dto.SupportH2C, out bool supportH2C) && supportH2C)
                        return version == "2.0";

                    return true; // Validation passes if SupportH2C is false or invalid
                })
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
                .Must(url =>
                {
                    // Allow valid URLs or placeholders
                    return string.IsNullOrEmpty(url)
                        || url.StartsWith("$")
                        || (Uri.TryCreate(url, UriKind.Absolute, out Uri result)
                            && (result.Scheme == Uri.UriSchemeHttp || result.Scheme == Uri.UriSchemeHttps));
                })
                .WithMessage("The 'URL' must be a valid URL according to RFC 3986 or a placeholder starting with '$'")
                .Must((dto, url) =>
                {
                    // Parse SupportH2C as bool and validate compatibility with URL schema
                    if (string.IsNullOrWhiteSpace(dto.SupportH2C) || dto.SupportH2C.StartsWith("$"))
                        return true;

                    if (bool.TryParse(dto.SupportH2C, out bool supportH2C) && supportH2C)
                    {
                        return string.IsNullOrEmpty(url)
                            || url.StartsWith("$")
                            || url.StartsWith("http://", StringComparison.OrdinalIgnoreCase);
                    }

                    return true; // Validation passes if SupportH2C is false or invalid
                })
                .WithMessage("H2C only works with the HTTP schema or placeholders starting with '$'");

            RuleFor(dto => dto.SupportH2C)
                .NotNull()
                .When(dto =>
                    !string.IsNullOrEmpty(dto.URL)
                    && dto.URL.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                    && !string.IsNullOrEmpty(dto.HttpVersion)
                    && dto.HttpVersion.Equals("2.0", StringComparison.OrdinalIgnoreCase))
                .WithMessage("'SupportH2C' must be a valid boolean ('true' or 'false') or a placeholder starting with '$'")
                .Must(supportH2C =>
                {
                    // Allow valid boolean values or placeholders
                    return string.IsNullOrEmpty(supportH2C)
                        || supportH2C.StartsWith("$")
                        || bool.TryParse(supportH2C, out _);
                })
                .WithMessage("'SupportH2C' must be 'true', 'false', or a placeholder starting with '$'");


            RuleFor(dto => dto.SaveResponse)
                .NotNull()
                .WithMessage("'Save Response' must be a valid boolean ('true' or 'false') or a placeholder starting with '$'")
                .Must(saveResponse =>
                {
                    // Allow valid boolean values or placeholders
                    return string.IsNullOrEmpty(saveResponse)
                        || saveResponse.StartsWith("$")
                        || bool.TryParse(saveResponse, out _);
                })
                .WithMessage("'Save Response' must be 'true', 'false', or a placeholder starting with '$'");


            RuleFor(dto => dto.DownloadHtmlEmbeddedResources)
                .NotNull()
                .WithMessage("'Download Html Embedded Resources' must be a valid boolean ('true' or 'false') or a placeholder starting with '$'")
                .Must(downloadHtmlEmbeddedResources =>
                {
                    // Allow valid boolean values or placeholders
                    return string.IsNullOrEmpty(downloadHtmlEmbeddedResources)
                        || downloadHtmlEmbeddedResources.StartsWith("$")
                        || bool.TryParse(downloadHtmlEmbeddedResources, out _);
                })
                .WithMessage("'Download Html Embedded Resources' must be 'true', 'false', or a placeholder starting with '$'");


            // Enforce HTTP when SupportH2C is true
            When(dto =>
            {
                // Parse SupportH2C as a boolean
                return !string.IsNullOrWhiteSpace(dto.SupportH2C)
                       && !dto.SupportH2C.StartsWith("$")
                       && bool.TryParse(dto.SupportH2C, out bool supportH2C)
                       && supportH2C;
            }, () =>
            {
                RuleFor(dto => dto.URL)
                    .Must(url =>
                    {
                        // Ensure URL uses the HTTP scheme when SupportH2C is enabled
                        return string.IsNullOrEmpty(url) || url.StartsWith("$") || url.StartsWith("http://", StringComparison.OrdinalIgnoreCase);
                    })
                    .WithMessage("When 'SupportH2C' is enabled, the 'URL' must use the HTTP scheme.");

                RuleFor(dto => dto.HttpVersion)
                    .Must(httpVersion =>
                    {
                        // Ensure HttpVersion is 2.0 when SupportH2C is enabled
                        return string.IsNullOrEmpty(httpVersion) || httpVersion.Equals("2.0", StringComparison.OrdinalIgnoreCase);
                    })
                    .WithMessage("When 'SupportH2C' is enabled, the 'HttpVersion' must be set to 2.0.");
            });

        }

        public override HttpRequestDto Dto { get { return _requestDto; } }
    }
}
