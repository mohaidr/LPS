using LPS.Domain;
using LPS.UI.Core.Build.Services;
using System;
using System.Collections.Generic;
using System.CommandLine.Binding;
using System.CommandLine;
using System.Linq;
using System.Text;
using System.CommandLine.Parsing;
using LPS.Domain.Domain.Common.Enums;
using LPS.DTOs;

namespace LPS.UI.Core.LPSCommandLine.Bindings
{
    public class CommandBinder : BinderBase<PlanDto>
    {
        private Option<string> _nameOption;
        private Option<string> _roundNameOption;
        private Option<int> _startupDelayOption;
        private Option<string> _httpIterationNameOption;
        private Option<int?> _requestCountOption;
        private Option<bool> _maximizeThroughputOption;
        private Option<int?> _duration;
        private Option<int?> _coolDownTime;
        private Option<int?> _batchSize;
        private Option<string> _httpMethodOption;
        private Option<bool> _downloadHtmlEmbeddedResourcesOption;
        private Option<bool> _saveResponseOption;
        private Option<bool?> _supportH2C;
        private Option<string> _httpversionOption;
        private Option<string> _urlOption;
        private Option<IList<string>> _headerOption;
        private Option<string> _payloadOption;
        Option<IterationMode> _iterationModeOption;
        private Option<int> _numberOfClientsOption;
        private Option<int?> _arrivalDelayOption;
        private Option<bool> _delayClientCreationOption;
        private Option<bool> _runInParallerOption;

        public CommandBinder(
            Option<string>? nameOption = null,
            Option<string>? roundNameOption = null,
            Option<int>? startupDelayOption = null,
            Option<int>? numberOfClientsOption = null,
            Option<int?>? arrivalDelayOption = null,
            Option<bool>? delayClientCreationOption = null,
            Option<bool>? runInParallerOption = null,
            Option<string>? httpIterationNameOption = null,
            Option<int?>? requestCountOption = null,
            Option<IterationMode>? iterationModeOption = null,
            Option<int?>? duratiion = null,
            Option<int?>? coolDownTime = null,
            Option<bool>? maximizeThroughput = null,
            Option<int?>? batchSizeOption = null,
            Option<string>? httpMethodOption = null,
            Option<string>? httpversionOption = null,
            Option<string>? urlOption = null,
            Option<IList<string>>? headerOption = null,
            Option<string>? payloadOption = null,
            Option<bool>? downloadHtmlEmbeddedResourcesOption = null,
            Option<bool>? saveResponseOption = null,
            Option<bool?>? supportH2C = null)
        {
            _nameOption = nameOption ?? CommandLineOptions.LPSCommandOptions.PlanNameOption;
            _roundNameOption = roundNameOption ?? CommandLineOptions.LPSCommandOptions.RoundNameOption;
            _startupDelayOption = startupDelayOption?? CommandLineOptions.LPSCommandOptions.StartupDelayOption;
            _numberOfClientsOption = numberOfClientsOption ?? CommandLineOptions.LPSCommandOptions.NumberOfClientsOption;
            _arrivalDelayOption = arrivalDelayOption ?? CommandLineOptions.LPSCommandOptions.ArrivalDelayOption;
            _delayClientCreationOption = delayClientCreationOption ?? CommandLineOptions.LPSCommandOptions.DelayClientCreationOption;
            _runInParallerOption = runInParallerOption ?? CommandLineOptions.LPSCommandOptions.RunInParallelOption;
            _httpIterationNameOption = httpIterationNameOption ?? CommandLineOptions.LPSCommandOptions.IterationNameOption;
            _iterationModeOption = iterationModeOption ?? CommandLineOptions.LPSCommandOptions.IterationModeOption;
            _duration = duratiion ?? CommandLineOptions.LPSCommandOptions.Duration;
            _batchSize = batchSizeOption ?? CommandLineOptions.LPSCommandOptions.BatchSize;
            _coolDownTime = coolDownTime ?? CommandLineOptions.LPSCommandOptions.CoolDownTime;
            _requestCountOption = requestCountOption ?? CommandLineOptions.LPSCommandOptions.RequestCountOption;
            _maximizeThroughputOption = maximizeThroughput ?? CommandLineOptions.LPSCommandOptions.MaximizeThroughputOption;
            _httpMethodOption = httpMethodOption ?? CommandLineOptions.LPSCommandOptions.HttpMethodOption;
            _httpversionOption = httpversionOption ?? CommandLineOptions.LPSCommandOptions.HttpVersionOption;
            _urlOption = urlOption ?? CommandLineOptions.LPSCommandOptions.UrlOption;
            _headerOption = headerOption ?? CommandLineOptions.LPSCommandOptions.HeaderOption;
            _payloadOption = payloadOption ?? CommandLineOptions.LPSCommandOptions.PayloadOption;
            _downloadHtmlEmbeddedResourcesOption = downloadHtmlEmbeddedResourcesOption ?? CommandLineOptions.LPSCommandOptions.DownloadHtmlEmbeddedResources;
            _saveResponseOption = saveResponseOption ?? CommandLineOptions.LPSCommandOptions.SaveResponse;
            _supportH2C = supportH2C ?? CommandLineOptions.LPSCommandOptions.SupportH2C;
        }

        protected override PlanDto GetBoundValue(BindingContext bindingContext)
        {
            return new PlanDto()
            {
                Name = bindingContext.ParseResult.GetValueForOption(_nameOption),
                Rounds = new List<RoundDto>()
                {
                    new RoundDto()
                    {
                        Name = bindingContext.ParseResult.GetValueForOption(_roundNameOption),
                        StartupDelay = bindingContext.ParseResult.GetValueForOption(_startupDelayOption),
                        NumberOfClients = bindingContext.ParseResult.GetValueForOption(_numberOfClientsOption),
                        ArrivalDelay = bindingContext.ParseResult.GetValueForOption(_arrivalDelayOption),
                        DelayClientCreationUntilIsNeeded = bindingContext.ParseResult.GetValueForOption(_delayClientCreationOption),
                        RunInParallel = bindingContext.ParseResult.GetValueForOption(_runInParallerOption),
                        Iterations = new List<HttpIterationDto>()
                        {
                            new()
                            {
                                Name = bindingContext.ParseResult.GetValueForOption(_httpIterationNameOption),
                                Mode = bindingContext.ParseResult.GetValueForOption(_iterationModeOption),
                                RequestCount = bindingContext.ParseResult.GetValueForOption(_requestCountOption),
                                MaximizeThroughput = bindingContext.ParseResult.GetValueForOption(_maximizeThroughputOption),
                                Duration = bindingContext.ParseResult.GetValueForOption(_duration),
                                CoolDownTime = bindingContext.ParseResult.GetValueForOption(_coolDownTime),
                                BatchSize = bindingContext.ParseResult.GetValueForOption(_batchSize),
                                RequestProfile = new HttpRequestProfileDto()
                                {
                                    HttpMethod = bindingContext.ParseResult.GetValueForOption(_httpMethodOption),
                                    HttpVersion = bindingContext.ParseResult.GetValueForOption(_httpversionOption),
                                    DownloadHtmlEmbeddedResources = bindingContext.ParseResult.GetValueForOption(_downloadHtmlEmbeddedResourcesOption),
                                    SaveResponse = bindingContext.ParseResult.GetValueForOption(_saveResponseOption),
                                    SupportH2C = bindingContext.ParseResult.GetValueForOption(_supportH2C),
                                    URL = bindingContext.ParseResult.GetValueForOption(_urlOption),
                                    Payload = !string.IsNullOrEmpty(bindingContext.ParseResult.GetValueForOption(_payloadOption)) ? InputPayloadService.Parse(bindingContext.ParseResult.GetValueForOption(_payloadOption)) : string.Empty,
                                    HttpHeaders = InputHeaderService.Parse(bindingContext.ParseResult.GetValueForOption(_headerOption)),
                                },
                            }
                        }   
                    } 
                }
            };
        }
    }
}
