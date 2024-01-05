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
    public class LPSRunCommandBinder : BinderBase<LPSHttpRun.SetupCommand>
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
        Option<LPSHttpRun.IterationMode> _iterationModeOption;

        public LPSRunCommandBinder(Option<string> nameOption = null,
            Option<int?> requestCountOption = null,
            Option<LPSHttpRun.IterationMode> iterationModeOption = null,
            Option<int?> duratiion = null,
            Option<int?> coolDownTime = null,
            Option<int?> batchSizeOption = null,
            Option<string> httpMethodOption = null,
            Option<string> httpversionOption = null,
            Option<string> urlOption = null,
            Option<IList<string>> headerOption = null,
            Option<string> payloadOption = null,
            Option<bool> downloadHtmlEmbeddedResourcesOption = null,
            Option<bool> saveResponseOption = null)
        {
            _nameOption = nameOption ?? LPSCommandLineOptions.LPSAddCommandOptions.RunNameOption;
            _iterationModeOption = iterationModeOption ?? LPSCommandLineOptions.LPSAddCommandOptions.IterationModeOption;
            _duration = duratiion ?? LPSCommandLineOptions.LPSAddCommandOptions.Duratiion;
            _batchSize = batchSizeOption ?? LPSCommandLineOptions.LPSAddCommandOptions.BatchSize;
            _coolDownTime = coolDownTime ?? LPSCommandLineOptions.LPSAddCommandOptions.CoolDownTime;
            _requestCountOption = requestCountOption ?? LPSCommandLineOptions.LPSAddCommandOptions.RequestCountOption;
            _httpMethodOption = httpMethodOption ?? LPSCommandLineOptions.LPSAddCommandOptions.HttpMethodOption;
            _httpversionOption = httpversionOption ?? LPSCommandLineOptions.LPSAddCommandOptions.HttpversionOption;
            _urlOption = urlOption ?? LPSCommandLineOptions.LPSAddCommandOptions.UrlOption;
            _headerOption = headerOption ?? LPSCommandLineOptions.LPSAddCommandOptions.HeaderOption;
            _payloadOption = payloadOption ?? LPSCommandLineOptions.LPSAddCommandOptions.PayloadOption;
            _downloadHtmlEmbeddedResourcesOption = downloadHtmlEmbeddedResourcesOption ?? LPSCommandLineOptions.LPSAddCommandOptions.DownloadHtmlEmbeddedResources;
            _saveResponseOption = saveResponseOption ?? LPSCommandLineOptions.LPSAddCommandOptions.SaveResponse;
        }

        protected override LPSHttpRun.SetupCommand GetBoundValue(BindingContext bindingContext) =>
            new LPSHttpRun.SetupCommand
            {
                Name = bindingContext.ParseResult.GetValueForOption(_nameOption),
                Mode = bindingContext.ParseResult.GetValueForOption(_iterationModeOption),
                RequestCount = bindingContext.ParseResult.GetValueForOption(_requestCountOption),
                Duration = bindingContext.ParseResult.GetValueForOption(_duration),
                CoolDownTime = bindingContext.ParseResult.GetValueForOption(_coolDownTime),
                BatchSize = bindingContext.ParseResult.GetValueForOption(_batchSize),
                LPSRequestProfile = new LPSHttpRequestProfile.SetupCommand()
                {
                    HttpMethod = bindingContext.ParseResult.GetValueForOption(_httpMethodOption),
                    Httpversion = bindingContext.ParseResult.GetValueForOption(_httpversionOption),
                    DownloadHtmlEmbeddedResources = bindingContext.ParseResult.GetValueForOption(_downloadHtmlEmbeddedResourcesOption),
                    SaveResponse = bindingContext.ParseResult.GetValueForOption(_saveResponseOption),
                    URL = bindingContext.ParseResult.GetValueForOption(_urlOption),
                    Payload = !string.IsNullOrEmpty(bindingContext.ParseResult.GetValueForOption(_payloadOption)) ?
                    InputPayloadService.Parse(bindingContext.ParseResult.GetValueForOption(_payloadOption)) : string.Empty,
                    HttpHeaders = InputHeaderService.Parse(bindingContext.ParseResult.GetValueForOption(_headerOption)),
                },
            };
    }
}
