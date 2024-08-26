using LPS.Domain;
using LPS.UI.Core.UI.Build.Services;
using System;
using System.Collections.Generic;
using System.CommandLine.Binding;
using System.CommandLine;
using System.Linq;
using System.Text;
using System.CommandLine.Parsing;

namespace LPS.UI.Core.LPSCommandLine.Bindings
{
    public class RunCommandBinder : BinderBase<HttpRun.SetupCommand>
    {
        private Option<string> _nameOption;
        private Option<int?> _requestCountOption;
        private Option<int?> _duration;
        private Option<int?> _coolDownTime;
        private Option<int?> _batchSize;
        private Option<string> _httpMethodOption;
        private Option<bool> _downloadHtmlEmbeddedResourcesOption;
        private Option<bool> _saveResponseOption;
        private Option<string> _httpversionOption;
        private Option<string> _urlOption;
        private Option<IList<string>> _headerOption;
        private Option<string> _payloadOption;
        Option<HttpRun.IterationMode> _iterationModeOption;

        public RunCommandBinder(Option<string>? nameOption = null,
            Option<int?>? requestCountOption = null,
            Option<HttpRun.IterationMode>? iterationModeOption = null,
            Option<int?>? duratiion = null,
            Option<int?>? coolDownTime = null,
            Option<int?>? batchSizeOption = null,
            Option<string>? httpMethodOption = null,
            Option<string>? httpversionOption = null,
            Option<string>? urlOption = null,
            Option<IList<string>>? headerOption = null,
            Option<string>? payloadOption = null,
            Option<bool>? downloadHtmlEmbeddedResourcesOption = null,
            Option<bool>? saveResponseOption = null)
        {
            _nameOption = nameOption ?? CommandLineOptions.LPSAddCommandOptions.RunNameOption;
            _iterationModeOption = iterationModeOption ?? CommandLineOptions.LPSAddCommandOptions.IterationModeOption;
            _duration = duratiion ?? CommandLineOptions.LPSAddCommandOptions.Duratiion;
            _batchSize = batchSizeOption ?? CommandLineOptions.LPSAddCommandOptions.BatchSize;
            _coolDownTime = coolDownTime ?? CommandLineOptions.LPSAddCommandOptions.CoolDownTime;
            _requestCountOption = requestCountOption ?? CommandLineOptions.LPSAddCommandOptions.RequestCountOption;
            _httpMethodOption = httpMethodOption ?? CommandLineOptions.LPSAddCommandOptions.HttpMethodOption;
            _httpversionOption = httpversionOption ?? CommandLineOptions.LPSAddCommandOptions.HttpversionOption;
            _urlOption = urlOption ?? CommandLineOptions.LPSAddCommandOptions.UrlOption;
            _headerOption = headerOption ?? CommandLineOptions.LPSAddCommandOptions.HeaderOption;
            _payloadOption = payloadOption ?? CommandLineOptions.LPSAddCommandOptions.PayloadOption;
            _downloadHtmlEmbeddedResourcesOption = downloadHtmlEmbeddedResourcesOption ?? CommandLineOptions.LPSAddCommandOptions.DownloadHtmlEmbeddedResources;
            _saveResponseOption = saveResponseOption ?? CommandLineOptions.LPSAddCommandOptions.SaveResponse;
        }

        protected override HttpRun.SetupCommand GetBoundValue(BindingContext bindingContext) =>
            new HttpRun.SetupCommand
            {
                Name = bindingContext.ParseResult.GetValueForOption(_nameOption),
                Mode = bindingContext.ParseResult.GetValueForOption(_iterationModeOption),
                RequestCount = bindingContext.ParseResult.GetValueForOption(_requestCountOption),
                Duration = bindingContext.ParseResult.GetValueForOption(_duration),
                CoolDownTime = bindingContext.ParseResult.GetValueForOption(_coolDownTime),
                BatchSize = bindingContext.ParseResult.GetValueForOption(_batchSize),
                LPSRequestProfile = new HttpRequestProfile.SetupCommand()
                {
                    HttpMethod = bindingContext.ParseResult.GetValueForOption(_httpMethodOption),
                    Httpversion = bindingContext.ParseResult.GetValueForOption(_httpversionOption),
                    DownloadHtmlEmbeddedResources = bindingContext.ParseResult.GetValueForOption(_downloadHtmlEmbeddedResourcesOption),
                    SaveResponse = bindingContext.ParseResult.GetValueForOption(_saveResponseOption),
                    URL = bindingContext.ParseResult.GetValueForOption(_urlOption),
                    Payload = !string.IsNullOrEmpty(bindingContext.ParseResult.GetValueForOption(_payloadOption)) ? InputPayloadService.Parse(bindingContext.ParseResult.GetValueForOption(_payloadOption)) : string.Empty,
                    HttpHeaders = InputHeaderService.Parse(bindingContext.ParseResult.GetValueForOption(_headerOption)),
                },
            };
    }
}
