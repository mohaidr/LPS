using AutoMapper;
using LPS.Domain;
using LPS.Domain.Common.Interfaces;
using LPS.Domain.Domain.Common.Enums;
using LPS.Domain.LPSFlow.LPSHandlers;
using LPS.DTOs;


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
                .ForMember(dest => dest.ArrivalDelay, opt => opt.MapFrom(src => ResolvePlaceholderAsync<int?>(src.ArrivalDelay).Result))
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
                .ForMember(dest => dest.Id, opt => opt.Ignore()) // Ignore unmapped properties
                .ForMember(dest => dest.IsValid, opt => opt.Ignore())
                .ForMember(dest => dest.ValidationErrors, opt => opt.Ignore());

            // Map HttpRequestDto to HttpRequest.SetupCommand
            // Do not apply placeholder resolver on (HttpMethod, HttpVersion, URL, HttpHeaders); the values should be resolved instantly when sending the request.
            CreateMap<HttpRequestDto, HttpRequest.SetupCommand>()
                .ForMember(dest => dest.URL, opt => opt.MapFrom(src => src.URL)) 
                .ForMember(dest => dest.HttpMethod, opt => opt.MapFrom(src => src.HttpMethod))
                .ForMember(dest => dest.HttpVersion, opt => opt.MapFrom(src => src.HttpVersion))
                .ForMember(dest => dest.HttpHeaders, opt => opt.MapFrom(src => src.HttpHeaders.ToDictionary(
                    kvp => kvp.Key,
                    kvp => kvp.Value)))
                .ForMember(dest => dest.Payload, opt => opt.MapFrom(src => src.Payload))
                .ForMember(dest => dest.DownloadHtmlEmbeddedResources, opt => opt.MapFrom(src => ResolvePlaceholderAsync<bool>(src.DownloadHtmlEmbeddedResources).Result))
                .ForMember(dest => dest.SaveResponse, opt => opt.MapFrom(src => ResolvePlaceholderAsync<bool>(src.SaveResponse).Result))
                .ForMember(dest => dest.SupportH2C, opt => opt.MapFrom(src => ResolvePlaceholderAsync<bool>(src.SupportH2C).Result))
                .ForMember(dest => dest.Id, opt => opt.Ignore()) // Ignore unmapped properties
                .ForMember(dest => dest.IsValid, opt => opt.Ignore())
                .ForMember(dest => dest.ValidationErrors, opt => opt.Ignore());

            // Map CaptureHandlerDto to CaptureHandler.SetupCommand
            CreateMap<CaptureHandlerDto, CaptureHandler.SetupCommand>()
                .ForMember(dest => dest.Name, opt => opt.MapFrom(src => ResolvePlaceholderAsync<string>(src.Name).Result))
                .ForMember(dest => dest.As, opt => opt.MapFrom(src => ResolvePlaceholderAsync<string>(src.As).Result))
                .ForMember(dest => dest.MakeGlobal, opt => opt.MapFrom(src => ResolvePlaceholderAsync<bool>(src.MakeGlobal).Result))
                .ForMember(dest => dest.Regex, opt => opt.MapFrom(src => ResolvePlaceholderAsync<string>(src.Regex).Result))
                .ForMember(dest => dest.Headers, opt => opt.MapFrom(src => src.Headers.Select(header => ResolvePlaceholderAsync<string>(header).Result).ToList()))
                .ForMember(dest => dest.Id, opt => opt.Ignore()) // Ignore unmapped properties
                .ForMember(dest => dest.IsValid, opt => opt.Ignore())
            .ForMember(dest => dest.ValidationErrors, opt => opt.Ignore());
        }

        private async Task<T> ResolvePlaceholderAsync<T>(string value)
        {
            return await _placeholderResolver.ResolvePlaceholdersAsync<T>(value, _sessionId, CancellationToken.None);
        }
    }

}
