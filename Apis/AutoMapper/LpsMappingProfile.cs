﻿using LPS.Domain.LPSSession;
using LPS.Domain;
using LPS.UI.Common.DTOs;
using Spectre.Console;
using AutoMapper;
using Profile = AutoMapper.Profile;
using HttpRequest = LPS.Domain.HttpRequest;

namespace Apis.AutoMapper
{
    public class LpsMappingProfile : Profile
    {
        public LpsMappingProfile()
        {
            // Plan -> PlanDto
            CreateMap<Plan, PlanDto>()
                .ForMember(d => d.Name, m => m.MapFrom(s => s.Name))
                // You can materialize rounds/iters later or via AfterMap if lazy
                .ForMember(d => d.Rounds, m => m.Ignore())
                .ForMember(d => d.Variables, m => m.Ignore())
                .ForMember(d => d.Environments, m => m.Ignore())
                .ForMember(d => d.Iterations, m => m.Ignore());

            // Round -> RoundDto
            CreateMap<Round, RoundDto>()
                .ForMember(d => d.Name, m => m.MapFrom(s => s.Name))
                .ForMember(d => d.BaseUrl, m => m.Ignore())
                .ForMember(d => d.StartupDelay, m => m.MapFrom(s => s.StartupDelay.ToString()))
                .ForMember(d => d.NumberOfClients, m => m.MapFrom(s => s.NumberOfClients.ToString()))
                .ForMember(d => d.ArrivalDelay, m => m.MapFrom(s => s.ArrivalDelay.HasValue ? s.ArrivalDelay.Value.ToString() : null))
                .ForMember(d => d.DelayClientCreationUntilIsNeeded, m => m.MapFrom(s => s.DelayClientCreationUntilIsNeeded.Value.ToString().ToLower()))
                .ForMember(d => d.RunInParallel, m => m.MapFrom(s => s.RunInParallel.Value.ToString().ToLower()))
                .ForMember(d => d.Tags, m => m.Ignore())
                .ForMember(d => d.Iterations, m => m.Ignore())
                .ForMember(d => d.ReferencedIterations, m => m.Ignore());

            // Iteration -> HttpIterationDto (only for HttpIteration)
            CreateMap<HttpIteration, HttpIterationDto>()
                .ForMember(d => d.Name, m => m.MapFrom(s => s.Name))
                .ForMember(d => d.StartupDelay, m => m.MapFrom(s => s.StartupDelay.ToString()))
                .ForMember(d => d.MaximizeThroughput, m => m.MapFrom(s => s.MaximizeThroughput.ToString().ToLower()))
                .ForMember(d => d.Mode, m => m.MapFrom(s => s.Mode.HasValue ? s.Mode.Value.ToString() : "R"))
                .ForMember(d => d.RequestCount, m => m.MapFrom(s => s.RequestCount.HasValue ? s.RequestCount.Value.ToString() : null))
                .ForMember(d => d.Duration, m => m.MapFrom(s => s.Duration.HasValue ? s.Duration.Value.ToString() : null))
                .ForMember(d => d.BatchSize, m => m.MapFrom(s => s.BatchSize.HasValue ? s.BatchSize.Value.ToString() : null))
                .ForMember(d => d.CoolDownTime, m => m.MapFrom(s => s.CoolDownTime.HasValue ? s.CoolDownTime.Value.ToString() : null))
                .ForMember(d => d.SkipIf, m => m.MapFrom(s => s.SkipIf))
                .ForMember(d => d.TerminationRules, m => m.MapFrom(s => s.TerminationRules != null
                    ? s.TerminationRules.Select(tr => new TerminationRuleDto
                    {
                        ErrorStatusCodes = tr.ErrorStatusCodes != null ? string.Join(",", tr.ErrorStatusCodes.Select(c => (int)c)) : string.Empty,
                        MaxErrorRate = tr.MaxErrorRate != null ? tr.MaxErrorRate.Value.ToString(): string.Empty,
                        GracePeriod = tr.GracePeriod!=null ? tr.GracePeriod.Value.ToString(): string.Empty,
                        MaxAvg = tr.MaxAvg != null ? tr.MaxAvg.Value.ToString(): string.Empty,
                        MaxP90 = tr.MaxP90 != null ? tr.MaxP90.Value.ToString(): string.Empty,
                        MaxP50 = tr.MaxP50 != null ? tr.MaxP50.Value.ToString(): string.Empty,
                        MaxP10 = tr.MaxP10 != null ? tr.MaxP10.Value.ToString(): string.Empty
                    }).ToList()
                    : new List<TerminationRuleDto>()))
                .ForMember(d => d.HttpRequest, m => m.MapFrom(s => s.HttpRequest))
                 .ForMember(d => d.FailureCriteria, m => m.MapFrom(s => new FailureCriteriaDto
                 {
                     MaxErrorRate = s.FailureCriteria.MaxErrorRate.HasValue ? s.FailureCriteria.MaxErrorRate.Value.ToString() : string.Empty,
                     ErrorStatusCodes = s.FailureCriteria.ErrorStatusCodes != null
                              ? string.Join(",", s.FailureCriteria.ErrorStatusCodes.Select(c => (int)c))
                              : string.Empty,
                     MaxP90 = s.FailureCriteria.MaxP90.HasValue ? s.FailureCriteria.MaxP90.Value.ToString() : string.Empty,
                     MaxP50 = s.FailureCriteria.MaxP50.HasValue ? s.FailureCriteria.MaxP50.Value.ToString() : string.Empty,
                     MaxP10 = s.FailureCriteria.MaxP10.HasValue ? s.FailureCriteria.MaxP10.Value.ToString() : string.Empty,
                     MaxAvg = s.FailureCriteria.MaxAvg.HasValue ? s.FailureCriteria.MaxAvg.Value.ToString() : string.Empty
                 }));

            // HttpRequest -> HttpRequestDto
            CreateMap<HttpRequest, HttpRequestDto>()
                .ForMember(d => d.URL, m => m.MapFrom(s => s.Url.Url))
                .ForMember(d => d.HttpMethod, m => m.MapFrom(s => s.HttpMethod))
                .ForMember(d => d.HttpVersion, m => m.MapFrom(s => s.HttpVersion))
                .ForMember(d => d.HttpHeaders, m => m.MapFrom(s => s.HttpHeaders))
                .ForMember(d => d.DownloadHtmlEmbeddedResources, m => m.MapFrom(s => s.DownloadHtmlEmbeddedResources.ToString().ToLower()))
                .ForMember(d => d.SaveResponse, m => m.MapFrom(s => s.SaveResponse.ToString().ToLower()))
                .ForMember(d => d.SupportH2C, m => m.MapFrom(s => s.SupportH2C.ToString().ToLower()))
                .ForMember(d => d.Capture, m => m.MapFrom(s => s.Capture))
                .ForMember(d => d.Payload, option => option.Ignore())
;

            // CaptureHandler -> CaptureHandlerDto
            CreateMap<LPS.Domain.LPSFlow.LPSHandlers.CaptureHandler, CaptureHandlerDto>()
                .ForMember(d => d.To, m => m.MapFrom(s => s.To))
                .ForMember(d => d.As, m => m.MapFrom(s => s.As))
                .ForMember(d => d.MakeGlobal, m => m.MapFrom(s => s.MakeGlobal.HasValue ? s.MakeGlobal.Value.ToString().ToLower() : "false"))
                .ForMember(d => d.Regex, m => m.MapFrom(s => s.Regex));

        }
    }

}


