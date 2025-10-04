﻿using AutoMapper;
using LPS.Domain;
using LPS.Domain.Common;
using LPS.Domain.Common.Interfaces;
using LPS.Domain.Domain.Common.Enums;
using LPS.Domain.LPSFlow.LPSHandlers;
using LPS.Domain.LPSRequest.LPSHttpRequest;
using LPS.Domain.LPSSession;
using LPS.UI.Common.DTOs;
using LPS.Infrastructure.Common;
using System.Net;
using System.Linq;


namespace LPS.AutoMapper
{

    public class DtoToCommandProfile : Profile
    {
        private readonly IPlaceholderResolverService _placeholderResolver;
        private readonly string _sessionId;

        public DtoToCommandProfile(IPlaceholderResolverService placeholderResolver, string sessionId)
        {
            _placeholderResolver = placeholderResolver ?? throw new ArgumentNullException(nameof(placeholderResolver));
            _sessionId = sessionId ?? throw new ArgumentNullException(nameof(sessionId));

            ConfigureMappings();
        }

        private void ConfigureMappings()
        {
            // Map PlanDto to Plan.SetupCommand
            CreateMap<PlanDto, Plan.SetupCommand>()
            .ForMember(dest => dest.Name, opt => opt.MapFrom(src => ResolvePlaceholderAsync<string>(src.Name).Result))
            .ForMember(dest => dest.Id, opt => opt.Ignore()) // Ignore unmapped properties
            .ForMember(dest => dest.IsValid, opt => opt.Ignore())
            .ForMember(dest => dest.ValidationErrors, opt => opt.Ignore());

            // Map RoundDto to Round.SetupCommand
            CreateMap<RoundDto, Round.SetupCommand>()
                .ForMember(dest => dest.Name, opt => opt.MapFrom(src => ResolvePlaceholderAsync<string>(src.Name).Result))
                .ForMember(dest => dest.StartupDelay, opt => opt.MapFrom(src => ResolvePlaceholderAsync<int>(src.StartupDelay).Result))
                .ForMember(dest => dest.NumberOfClients, opt => opt.MapFrom(src => ResolvePlaceholderAsync<int?>(src.NumberOfClients).Result))
                .ForMember(dest => dest.ArrivalDelay, opt => opt.MapFrom(src => ResolvePlaceholderAsync<int>(src.ArrivalDelay).Result))
                .ForMember(dest => dest.DelayClientCreationUntilIsNeeded, opt => opt.MapFrom(src => ResolvePlaceholderAsync<bool>(src.DelayClientCreationUntilIsNeeded).Result))
                .ForMember(dest => dest.RunInParallel, opt => opt.MapFrom(src => ResolvePlaceholderAsync<bool>(src.RunInParallel).Result))
                .ForMember(dest => dest.Tags, opt => opt.MapFrom(src => src.Tags.Select(tag => ResolvePlaceholderAsync<string>(tag).Result).ToList()))
                .ForMember(dest => dest.Id, opt => opt.Ignore()) // Ignore unmapped properties
                .ForMember(dest => dest.IsValid, opt => opt.Ignore())
                .ForMember(dest => dest.ValidationErrors, opt => opt.Ignore());

            // Map HttpIterationDto to HttpIteration.SetupCommand
            CreateMap<HttpIterationDto, HttpIteration.SetupCommand>()
                .ForMember(dest => dest.Name, opt => opt.MapFrom(src => ResolvePlaceholderAsync<string>(src.Name).Result))
                .ForMember(dest => dest.StartupDelay, opt => opt.MapFrom(src => ResolvePlaceholderAsync<int>(src.StartupDelay).Result))
                .ForMember(dest => dest.MaximizeThroughput, opt => opt.MapFrom(src => ResolvePlaceholderAsync<bool>(src.MaximizeThroughput).Result))
                .ForMember(dest => dest.Mode, opt => opt.MapFrom(src => ResolvePlaceholderAsync<IterationMode?>(src.Mode).Result))
                .ForMember(dest => dest.RequestCount, opt => opt.MapFrom(src => ResolvePlaceholderAsync<int?>(src.RequestCount).Result))
                .ForMember(dest => dest.Duration, opt => opt.MapFrom(src => ResolvePlaceholderAsync<int?>(src.Duration).Result))
                .ForMember(dest => dest.BatchSize, opt => opt.MapFrom(src => ResolvePlaceholderAsync<int?>(src.BatchSize).Result))
                .ForMember(dest => dest.CoolDownTime, opt => opt.MapFrom(src => ResolvePlaceholderAsync<int?>(src.CoolDownTime).Result))
                .ForMember(dest => dest.SkipIf, opt => opt.MapFrom(src => src.SkipIf))
                .ForMember(dest => dest.FailureCriteria, opt => opt.Ignore()) // we'll set it in AfterMap
                .ForMember(dest => dest.TerminationRules, opt => opt.Ignore()) // we'll set it in AfterMap
                .ForMember(dest => dest.Id, opt => opt.Ignore()) // Ignore unmapped properties
                .ForMember(dest => dest.IsValid, opt => opt.Ignore())
                .ForMember(dest => dest.ValidationErrors, opt => opt.Ignore())
                .AfterMap((HttpIterationDto src, HttpIteration.SetupCommand dest) =>
                {
                    // ----- FailureCriteria -----
                    var maxErrorRate = ResolvePlaceholderAsync<double?>(src.FailureCriteria.MaxErrorRate).Result;

                    List<HttpStatusCode>? codes = null;
                    var rawCodes = ResolvePlaceholderAsync<string>(src.FailureCriteria.ErrorStatusCodes).Result;
                    if (!string.IsNullOrWhiteSpace(rawCodes) && !rawCodes.StartsWith("$"))
                    {
                        codes = rawCodes
                            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                            .Select(code => ResolvePlaceholderAsync<HttpStatusCode>(code).Result)
                            .ToList();
                    }

                    var p90 = ResolvePlaceholderAsync<double?>(src.FailureCriteria.MaxP90).Result;
                    var p50 = ResolvePlaceholderAsync<double?>(src.FailureCriteria.MaxP50).Result;
                    var p10 = ResolvePlaceholderAsync<double?>(src.FailureCriteria.MaxP10).Result;
                    var avg = ResolvePlaceholderAsync<double?>(src.FailureCriteria.MaxAvg).Result;

                    dest.FailureCriteria = new FailureCriteria(codes, maxErrorRate, p90, p50, p10, avg);

                    // ----- TerminationRules -----
                    if (src.TerminationRules is not null && src.TerminationRules.Count > 0)
                    {
                        dest.TerminationRules = src.TerminationRules.Select(tr =>
                        {
                            List<HttpStatusCode>? trCodes = null;
                            var rawTrCodes = ResolvePlaceholderAsync<string>(tr.ErrorStatusCodes).Result;
                            if (!string.IsNullOrWhiteSpace(rawTrCodes) && !rawTrCodes.StartsWith("$"))
                            {
                                trCodes = rawTrCodes
                                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                                    .Select(code => ResolvePlaceholderAsync<HttpStatusCode>(code).Result)
                                    .ToList();
                            }

                            return new TerminationRule(
                                trCodes,
                                ResolvePlaceholderAsync<TimeSpan?>(tr.GracePeriod).Result,
                                ResolvePlaceholderAsync<double?>(tr.MaxErrorRate).Result,
                                ResolvePlaceholderAsync<double?>(tr.MaxP90).Result,
                                ResolvePlaceholderAsync<double?>(tr.MaxP50).Result,
                                ResolvePlaceholderAsync<double?>(tr.MaxP10).Result,
                                ResolvePlaceholderAsync<double?>(tr.MaxAvg).Result
                            );
                        }).ToList();
                    }
                    else
                    {
                        dest.TerminationRules = new List<TerminationRule>();
                    }
                });

            // Map HttpRequestDto to HttpRequest.SetupCommand
            // Do not apply placeholder resolver on (HttpMethod, HttpVersion, URL, HttpHeaders); the values should be resolved instantly when sending the request.
            CreateMap<HttpRequestDto, HttpRequest.SetupCommand>()
                .ForMember(dest => dest.Url, opt => opt.MapFrom(src => ResolveURLShemaAndHostName(new URL(src.URL))))
                .ForMember(dest => dest.HttpMethod, opt => opt.MapFrom(src => src.HttpMethod))
                .ForMember(dest => dest.HttpVersion, opt => opt.MapFrom(src => src.HttpVersion))
                .ForMember(dest => dest.HttpHeaders, opt => opt.MapFrom(src => src.HttpHeaders.ToDictionary(
                    kvp => kvp.Key,
                    kvp => kvp.Value)))
                .ForMember(dest => dest.Payload, opt => opt.MapFrom(src => MapPayload(src.Payload)))
                .ForMember(dest => dest.DownloadHtmlEmbeddedResources, opt => opt.MapFrom(src => ResolvePlaceholderAsync<bool>(src.DownloadHtmlEmbeddedResources).Result))
                .ForMember(dest => dest.SaveResponse, opt => opt.MapFrom(src => ResolvePlaceholderAsync<bool>(src.SaveResponse).Result))
                .ForMember(dest => dest.SupportH2C, opt => opt.MapFrom(src => ResolvePlaceholderAsync<bool>(src.SupportH2C).Result))
                .ForMember(dest => dest.Id, opt => opt.Ignore()) // Ignore unmapped properties
                .ForMember(dest => dest.IsValid, opt => opt.Ignore())
                .ForMember(dest => dest.ValidationErrors, opt => opt.Ignore());

            // Map CaptureHandlerDto to CaptureHandler.SetupCommand
            CreateMap<CaptureHandlerDto, CaptureHandler.SetupCommand>()
                .ForMember(dest => dest.To, opt => opt.MapFrom(src => ResolvePlaceholderAsync<string>(src.To).Result))
                .ForMember(dest => dest.As, opt => opt.MapFrom(src => ResolvePlaceholderAsync<string>(src.As).Result))
                .ForMember(dest => dest.MakeGlobal, opt => opt.MapFrom(src => ResolvePlaceholderAsync<bool>(src.MakeGlobal).Result))
                .ForMember(dest => dest.Regex, opt => opt.MapFrom(src => ResolvePlaceholderAsync<string>(src.Regex).Result))
                .ForMember(dest => dest.Id, opt => opt.Ignore()) // Ignore unmapped properties
                .ForMember(dest => dest.IsValid, opt => opt.Ignore())
            .ForMember(dest => dest.ValidationErrors, opt => opt.Ignore());
        }

        private Payload MapPayload(PayloadDto payloadDto)
        {
            if (payloadDto == null)
                return Payload.CreateRaw(string.Empty);

            if (payloadDto.Type == null) // set to the default if null. the type is defined as nullable so it does not default to the first enum value.
            {
                if (!string.IsNullOrEmpty(payloadDto.File))
                {
                    payloadDto.Type = Payload.PayloadType.Binary;
                }
                else if (payloadDto.Multipart != null)
                {
                    payloadDto.Type = Payload.PayloadType.Multipart;
                }
                else
                {
                    payloadDto.Type = Payload.PayloadType.Raw;
                }
            }

            switch (payloadDto.Type)
            {
                case Payload.PayloadType.Raw:
                    return Payload.CreateRaw(payloadDto.Raw ?? string.Empty); // do not use resolver here, we resolve the raw payload during the session

                case Payload.PayloadType.Binary:
                    // Read the file content from the full path and create binary payload
                    string fullPath = Path.GetFullPath(ResolvePlaceholderAsync<string>(payloadDto.File).Result, AppConstants.EnvironmentCurrentDirectory);
                    if (string.IsNullOrEmpty(fullPath) || !File.Exists(fullPath))
                    {
                        throw new ArgumentException($"Invalid file path specified: {payloadDto.File}");
                    }
                    var binaryContent = File.ReadAllBytes(fullPath);
                    return Payload.CreateBinary(binaryContent);

                case Payload.PayloadType.Multipart:
                    // Map fields and files for multipart content
                    var fields = payloadDto.Multipart?.Fields?
                        .Select(field => new TextField(field.Name, field.Value, string.IsNullOrWhiteSpace(field.ContentType) ? "text/plain": field.ContentType))
                        .ToList();
                    var files = payloadDto.Multipart?.Files
                        .Select(file =>
                        {
                            // Resolve placeholders for file properties
                            string fullPath = Path.GetFullPath(ResolvePlaceholderAsync<string>(file.Path).Result, AppConstants.EnvironmentCurrentDirectory);
                            if (string.IsNullOrEmpty(fullPath) || !File.Exists(fullPath))
                            {
                                throw new ArgumentException($"Invalid file path specified for multipart file: {file.Name}");
                            }
                            string multiPartFileName = ResolvePlaceholderAsync<string>(file.Name).Result;
                            string extension = Path.GetExtension(fullPath).ToLowerInvariant();

                            multiPartFileName = string.IsNullOrEmpty(multiPartFileName) ? Path.GetFileName(fullPath).ToLowerInvariant() : multiPartFileName;
                            string contentType = ResolvePlaceholderAsync<string>(file.ContentType).Result;

                            if (string.IsNullOrEmpty(contentType))
                            {
                                contentType = MimeTypeExtensions.FromFileExtension(extension).ToContentType();
                            }
                            // Determine MIME type
                            MimeType mimeType = MimeTypeExtensions.FromContentType(contentType.Trim());

                            // Read file content based on MIME type
                            object fileContent = mimeType.IsTextContent()
                                ? File.ReadAllText(fullPath) // Read as text for text-based MIME types
                                : File.ReadAllBytes(fullPath); // Read as binary for other types

                            // Return the FileField instance
                            return new FileField(multiPartFileName, contentType, fileContent);
                        })
                        .ToList();

                    return Payload.CreateMultipart(fields, files);

                default:
                    throw new InvalidOperationException($"Unsupported payload type: {payloadDto.Type}");
            }
        }
        private URL ResolveURLShemaAndHostName(URL url)
        {
            string resolvedUrl = string.Concat(ResolvePlaceholderAsync<string>(url.Schema).Result, $"{(!string.IsNullOrEmpty(url.Schema) ? "://" : string.Empty)}", ResolvePlaceholderAsync<string>(url.HostName).Result, $"{(!string.IsNullOrEmpty(url.GetCombinedPathParameters()) ? "/" : string.Empty)}", url.GetCombinedPathParameters(), $"{(!string.IsNullOrEmpty(url.GetCombinedQueryParameters()) ? "?" : string.Empty)}", url.GetCombinedQueryParameters());
            return new URL(resolvedUrl);
        }
        private async Task<T> ResolvePlaceholderAsync<T>(string value)
        {
            return await _placeholderResolver.ResolvePlaceholdersAsync<T>(value, _sessionId, CancellationToken.None);
        }
    }
}
