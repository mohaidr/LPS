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
    public class LPSTestCaseCommandBinder : BinderBase<LPSHttpTestCase.SetupCommand>
    {
        private readonly Option<string> _nameOption;
        private readonly Option<int?> _requestCountOption;
        public static Option<int?> _duration;
        public static Option<int?> _coolDownTime;
        public static Option<int?> _batchSize;
        private readonly Option<string> _httpMethodOption;
        private readonly Option<bool> _downloadHtmlEmbeddedResourcesOption;
        private readonly Option<bool> _saveResponseOption;
        private readonly Option<string> _httpversionOption;
        private readonly Option<string> _urlOption;
        private readonly Option<IList<string>> _headerOption;
        private readonly Option<string> _payloadOption;
        Option<LPSHttpTestCase.IterationMode> _iterationModeOption;

        public LPSTestCaseCommandBinder(Option<string> nameOption,
            Option<int?> requestCountOption,
            Option<LPSHttpTestCase.IterationMode> iterationModeOption,
            Option<int?> duratiion,
            Option<int?> coolDownTime,
            Option<int?> batchSizem,
            Option<string> httpMethodOption,
            Option<string> httpversionOption,
            Option<string> urlOption,
            Option<IList<string>> headerOption,
            Option<string> payloadOption,
            Option<bool> downloadHtmlEmbeddedResourcesOption,
            Option<bool> saveResponseOption)
        {
            _nameOption = nameOption;
            _iterationModeOption = iterationModeOption;
            _duration = duratiion;
            _batchSize = batchSizem;
            _coolDownTime = coolDownTime;
            _requestCountOption = requestCountOption;
            _httpMethodOption = httpMethodOption;
            _httpversionOption = httpversionOption;
            _urlOption = urlOption;
            _headerOption = headerOption;
            _payloadOption = payloadOption;
            _downloadHtmlEmbeddedResourcesOption = downloadHtmlEmbeddedResourcesOption;
            _saveResponseOption = saveResponseOption;
        }

        protected override LPSHttpTestCase.SetupCommand GetBoundValue(BindingContext bindingContext) =>
            new LPSHttpTestCase.SetupCommand
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
