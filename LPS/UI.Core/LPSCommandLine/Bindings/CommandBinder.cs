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
    public class CommandBinder : BinderBase<TestPlan.SetupCommand>
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
        Option<HttpRun.IterationMode> _iterationModeOption;
        private Option<string> _testPlanNameOption;
        private Option<int> _numberOfClientsOption;
        private Option<int> _rampupPeriodOption;
        private Option<bool> _delayClientCreationOption;
        private Option<bool> _runInParallerOption;

        public CommandBinder(Option<string>? testPlanNameOption = null,
            Option<int>? numberOfClientsOption = null,
            Option<int>? rampupPeriodOption = null,
            Option<bool>? delayClientCreationOption = null,
            Option<bool>? runInParallerOption = null,
            Option<string>? httpRunNameOption = null,
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
            _testPlanNameOption = testPlanNameOption ?? CommandLineOptions.LPSCommandOptions.TestNameOption;
            _numberOfClientsOption = numberOfClientsOption ?? CommandLineOptions.LPSCommandOptions.NumberOfClientsOption;
            _rampupPeriodOption = rampupPeriodOption ?? CommandLineOptions.LPSCommandOptions.RampupPeriodOption;
            _delayClientCreationOption = delayClientCreationOption ?? CommandLineOptions.LPSCommandOptions.DelayClientCreation;
            _runInParallerOption = runInParallerOption ?? CommandLineOptions.LPSCommandOptions.RunInParaller;
            _httpRunNameOption = httpRunNameOption ?? CommandLineOptions.LPSCommandOptions.RunNameOption;
            _iterationModeOption = iterationModeOption ?? CommandLineOptions.LPSCommandOptions.IterationModeOption;
            _duration = duratiion ?? CommandLineOptions.LPSCommandOptions.Duration;
            _batchSize = batchSizeOption ?? CommandLineOptions.LPSCommandOptions.BatchSize;
            _coolDownTime = coolDownTime ?? CommandLineOptions.LPSCommandOptions.CoolDownTime;
            _requestCountOption = requestCountOption ?? CommandLineOptions.LPSCommandOptions.RequestCountOption;
            _httpMethodOption = httpMethodOption ?? CommandLineOptions.LPSCommandOptions.HttpMethodOption;
            _httpversionOption = httpversionOption ?? CommandLineOptions.LPSCommandOptions.HttpversionOption;
            _urlOption = urlOption ?? CommandLineOptions.LPSCommandOptions.UrlOption;
            _headerOption = headerOption ?? CommandLineOptions.LPSCommandOptions.HeaderOption;
            _payloadOption = payloadOption ?? CommandLineOptions.LPSCommandOptions.PayloadOption;
            _downloadHtmlEmbeddedResourcesOption = downloadHtmlEmbeddedResourcesOption ?? CommandLineOptions.LPSCommandOptions.DownloadHtmlEmbeddedResources;
            _saveResponseOption = saveResponseOption ?? CommandLineOptions.LPSCommandOptions.SaveResponse;

        }

        protected override TestPlan.SetupCommand GetBoundValue(BindingContext bindingContext)
        {
            return new TestPlan.SetupCommand
            {
                Name = bindingContext.ParseResult.GetValueForOption(_testPlanNameOption),
                NumberOfClients = bindingContext.ParseResult.GetValueForOption(_numberOfClientsOption),
                RampUpPeriod = bindingContext.ParseResult.GetValueForOption(_rampupPeriodOption),
                DelayClientCreationUntilIsNeeded = bindingContext.ParseResult.GetValueForOption(_delayClientCreationOption),
                RunInParallel = bindingContext.ParseResult.GetValueForOption(_runInParallerOption),
                LPSRuns = new List<HttpRun.SetupCommand>()
                {
                    new HttpRun.SetupCommand()
                    {
                        Name = bindingContext.ParseResult.GetValueForOption(_httpRunNameOption),
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
                    }
                }
            };
        }
    }
}
