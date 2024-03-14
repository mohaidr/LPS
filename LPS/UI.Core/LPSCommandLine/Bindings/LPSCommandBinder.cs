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
    public class LPSCommandBinder : BinderBase<LPSTestPlan.SetupCommand>
    {
        private Option<string> _httpRunNameOption;
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
        private Option<string> _testPlanNameOption;
        private Option<int> _numberOfClientsOption;
        private Option<int> _rampupPeriodOption;
        private Option<bool> _delayClientCreationOption;
        private Option<bool> _runInParallerOption;

        public LPSCommandBinder(Option<string> testPlanNameOption = null,
            Option<int> numberOfClientsOption = null,
            Option<int> rampupPeriodOption = null,
            Option<bool> delayClientCreationOption = null,
            Option<bool> runInParallerOption = null,
            Option<string> httpRunNameOption = null,
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
            _testPlanNameOption = testPlanNameOption ?? LPSCommandLineOptions.RootCommandLineOptions.TestNameOption;
            _numberOfClientsOption = numberOfClientsOption ?? LPSCommandLineOptions.RootCommandLineOptions.NumberOfClientsOption;
            _rampupPeriodOption = rampupPeriodOption ?? LPSCommandLineOptions.RootCommandLineOptions.RampupPeriodOption;
            _delayClientCreationOption = delayClientCreationOption ?? LPSCommandLineOptions.RootCommandLineOptions.DelayClientCreation;
            _runInParallerOption = runInParallerOption ?? LPSCommandLineOptions.RootCommandLineOptions.RunInParaller;
            _httpRunNameOption = httpRunNameOption ?? LPSCommandLineOptions.RootCommandLineOptions.RunNameOption;
            _iterationModeOption = iterationModeOption ?? LPSCommandLineOptions.RootCommandLineOptions.IterationModeOption;
            _duration = duratiion ?? LPSCommandLineOptions.RootCommandLineOptions.Duratiion;
            _batchSize = batchSizeOption ?? LPSCommandLineOptions.RootCommandLineOptions.BatchSize;
            _coolDownTime = coolDownTime ?? LPSCommandLineOptions.RootCommandLineOptions.CoolDownTime;
            _requestCountOption = requestCountOption ?? LPSCommandLineOptions.RootCommandLineOptions.RequestCountOption;
            _httpMethodOption = httpMethodOption ?? LPSCommandLineOptions.RootCommandLineOptions.HttpMethodOption;
            _httpversionOption = httpversionOption ?? LPSCommandLineOptions.RootCommandLineOptions.HttpversionOption;
            _urlOption = urlOption ?? LPSCommandLineOptions.RootCommandLineOptions.UrlOption;
            _headerOption = headerOption ?? LPSCommandLineOptions.RootCommandLineOptions.HeaderOption;
            _payloadOption = payloadOption ?? LPSCommandLineOptions.RootCommandLineOptions.PayloadOption;
            _downloadHtmlEmbeddedResourcesOption = downloadHtmlEmbeddedResourcesOption ?? LPSCommandLineOptions.RootCommandLineOptions.DownloadHtmlEmbeddedResources;
            _saveResponseOption = saveResponseOption ?? LPSCommandLineOptions.RootCommandLineOptions.SaveResponse;

        }

        protected override LPSTestPlan.SetupCommand GetBoundValue(BindingContext bindingContext) =>
            new LPSTestPlan.SetupCommand
            {
                Name = bindingContext.ParseResult.GetValueForOption(_testPlanNameOption),
                NumberOfClients = bindingContext.ParseResult.GetValueForOption(_numberOfClientsOption),
                RampUpPeriod = bindingContext.ParseResult.GetValueForOption(_rampupPeriodOption),
                DelayClientCreationUntilIsNeeded = bindingContext.ParseResult.GetValueForOption(_delayClientCreationOption),
                RunInParallel = bindingContext.ParseResult.GetValueForOption(_runInParallerOption),
                LPSHttpRuns = new List<LPSHttpRun.SetupCommand>()
                {
                    new LPSHttpRun.SetupCommand()
                    {
                        Name = bindingContext.ParseResult.GetValueForOption(_httpRunNameOption),
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
                            Payload = !string.IsNullOrEmpty(bindingContext.ParseResult.GetValueForOption(_payloadOption)) ? InputPayloadService.Parse(bindingContext.ParseResult.GetValueForOption(_payloadOption)) : string.Empty,
                            HttpHeaders = InputHeaderService.Parse(bindingContext.ParseResult.GetValueForOption(_headerOption)),
                        },
                    }
                }
            };
    }
}
