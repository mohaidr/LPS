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
    public class IterationCommandBinder(Option<string>? nameOption = null,
        Option<int?>? requestCountOption = null,
        Option<IterationMode>? iterationModeOption = null,
        Option<bool>? maximizeThroughput = null,
        Option<int?>? duratiion = null,
        Option<int?>? coolDownTime = null,
        Option<int?>? batchSizeOption = null,
        Option<string>? httpMethodOption = null,
        Option<string>? httpversionOption = null,
        Option<string>? urlOption = null,
        Option<IList<string>>? headerOption = null,
        Option<string>? payloadOption = null,
        Option<bool>? downloadHtmlEmbeddedResourcesOption = null,
        Option<bool>? saveResponseOption = null,
        Option<bool?>? supportH2C = null) : BinderBase<HttpIterationDto>
    {
        readonly private Option<string> _nameOption = nameOption ?? CommandLineOptions.LPSIterationCommandOptions.IterationNameOption;
        readonly private Option<int?> _requestCountOption = requestCountOption ?? CommandLineOptions.LPSIterationCommandOptions.RequestCountOption;
        readonly private Option<bool> _maximizeThroughputOption = maximizeThroughput ?? CommandLineOptions.LPSIterationCommandOptions.MaximizeThroughputOption;
        readonly private Option<int?> _duration = duratiion ?? CommandLineOptions.LPSIterationCommandOptions.Duration;
        readonly private Option<int?> _coolDownTime = coolDownTime ?? CommandLineOptions.LPSIterationCommandOptions.CoolDownTime;
        readonly private Option<int?> _batchSize = batchSizeOption ?? CommandLineOptions.LPSIterationCommandOptions.BatchSize;
        readonly private Option<string> _httpMethodOption = httpMethodOption ?? CommandLineOptions.LPSIterationCommandOptions.HttpMethodOption;
        readonly private Option<bool> _downloadHtmlEmbeddedResourcesOption = downloadHtmlEmbeddedResourcesOption ?? CommandLineOptions.LPSIterationCommandOptions.DownloadHtmlEmbeddedResources;
        readonly private Option<bool> _saveResponseOption = saveResponseOption ?? CommandLineOptions.LPSIterationCommandOptions.SaveResponse;
        readonly private Option<bool?> _supportH2C = supportH2C ?? CommandLineOptions.LPSIterationCommandOptions.SupportH2C;
        readonly private Option<string> _httpversionOption = httpversionOption ?? CommandLineOptions.LPSIterationCommandOptions.HttpVersionOption;
        readonly private Option<string> _urlOption = urlOption ?? CommandLineOptions.LPSIterationCommandOptions.UrlOption;
        readonly private Option<IList<string>> _headerOption = headerOption ?? CommandLineOptions.LPSIterationCommandOptions.HeaderOption;
        readonly private Option<string> _payloadOption = payloadOption ?? CommandLineOptions.LPSIterationCommandOptions.PayloadOption;
        readonly Option<IterationMode> _iterationModeOption = iterationModeOption ?? CommandLineOptions.LPSIterationCommandOptions.IterationModeOption;

        protected override HttpIterationDto GetBoundValue(BindingContext bindingContext) =>
            new()
            {
                Name = bindingContext.ParseResult.GetValueForOption(_nameOption),
                Mode = bindingContext.ParseResult.GetValueForOption(_iterationModeOption),
                MaximizeThroughput = bindingContext.ParseResult.GetValueForOption(_maximizeThroughputOption),
                RequestCount = bindingContext.ParseResult.GetValueForOption(_requestCountOption),
                Duration = bindingContext.ParseResult.GetValueForOption(_duration),
                CoolDownTime = bindingContext.ParseResult.GetValueForOption(_coolDownTime),
                BatchSize = bindingContext.ParseResult.GetValueForOption(_batchSize),
                Session = new HttpSessionDto()
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
            };
    }
}
