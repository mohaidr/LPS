using LPS.Domain;
using LPS.UI.Core.UI.Build.Services;
using System;
using System.Collections.Generic;
using System.CommandLine.Binding;
using System.CommandLine;
using System.Linq;
using System.Text;
using System.CommandLine.Parsing;

namespace LPS.UI.Core.LPSCommandLine
{
    public class LPSTestCaseCommandBinder : BinderBase<LPSHttpTestCase.SetupCommand>
    {
        private readonly Option<string> _nameOption;
        private readonly Option<int?> _requestCountOption;
        public static Option<int?> _duration;
        public static Option<int?> _coolDownTime;
        public static Option<int?> _batchSize;
        private readonly Option<string> _httpMethodOption;
        private readonly Option<string> _httpversionOption;
        private readonly Option<string> _urlOption;
        private readonly Option<IList<string>> _headerOption;
        private readonly Option<string> _payloadOption;
        Option<LPSHttpTestCase.IterationMode> _iterationModeOption;

        public LPSTestCaseCommandBinder(Option<string> nameOption, Option<int?> requestCountOption,
            Option<LPSHttpTestCase.IterationMode> iterationModeOption,
            Option<int?> duratiion,
            Option<int?> coolDownTime,
            Option<int?> batchSizem,
            Option<string> httpMethodOption,
            Option<string> httpversionOption,
            Option<string> urlOption,
            Option<IList<string>> headerOption,
            Option<string> payloadOption)
        {
            _nameOption = nameOption;
            _iterationModeOption = iterationModeOption;
            _duration = duratiion;
            _batchSize = batchSizem;
            _coolDownTime = coolDownTime;

            #region validators
            _iterationModeOption.AddValidator(result =>
            {
                LPSHttpTestCase.IterationMode? mode = result.GetValueOrDefault<LPSHttpTestCase.IterationMode?>();
                Console.WriteLine(mode);
                int? requestCount = result.Parent.Children.OfType<OptionResult>()
                    .FirstOrDefault(r => r.Symbol == _requestCountOption)?.GetValueOrDefault<int?>();
                int? duration = result.Parent.Children.OfType<OptionResult>()
                    .FirstOrDefault(r => r.Symbol == _duration)?.GetValueOrDefault<int?>();
                int? batchSize = result.Parent.Children.OfType<OptionResult>()
                    .FirstOrDefault(r => r.Symbol == _batchSize)?.GetValueOrDefault<int?>();
                int? coolDownTime = result.Parent.Children.OfType<OptionResult>()
                    .FirstOrDefault(r => r.Symbol == _coolDownTime)?.GetValueOrDefault<int?>();

                bool invalidIterationModeCombination = false;
                if (mode.Value == LPSHttpTestCase.IterationMode.DCB)
                {
                    if (!duration.HasValue || duration.Value <= 0
                        || !coolDownTime.HasValue || coolDownTime.Value <= 0
                        || !batchSize.HasValue || batchSize.Value <= 0
                        || requestCount.HasValue)
                    {
                        invalidIterationModeCombination = true;
                    }
                }
                else if (mode.Value == LPSHttpTestCase.IterationMode.CRB)
                {
                    if (!coolDownTime.HasValue || coolDownTime.Value <= 0
                        || !requestCount.HasValue || requestCount.Value <= 0
                        || !batchSize.HasValue || batchSize.Value <= 0
                        || duration.HasValue)
                    {
                        invalidIterationModeCombination = true;
                    }
                }
                else if (mode.Value == LPSHttpTestCase.IterationMode.CB)
                {
                    if (!coolDownTime.HasValue || coolDownTime.Value <= 0
                        || !batchSize.HasValue || batchSize.Value <= 0
                        || duration.HasValue
                        || requestCount.HasValue)
                    {
                        invalidIterationModeCombination = true;

                    }
                }
                else if (mode.Value == LPSHttpTestCase.IterationMode.R)
                {
                    if (!requestCount.HasValue || requestCount.Value <= 0
                        || duration.HasValue
                        || batchSize.HasValue
                        || coolDownTime.HasValue)
                    {

                        invalidIterationModeCombination = true;
                    }

                }
                else if (mode.Value == LPSHttpTestCase.IterationMode.D)
                {
                    if (!duration.HasValue || duration.Value <= 0
                        || requestCount.HasValue
                        || batchSize.HasValue
                        || coolDownTime.HasValue)
                    {

                        invalidIterationModeCombination = true;

                    }
                }
                if (invalidIterationModeCombination == true)
                {
                    Console.WriteLine("Invalid combination, you have to use one of the below combinations");
                    Console.WriteLine("\t- Duration && Cool Down Time && Batch Size");
                    Console.WriteLine("\t- Cool Down Time && Number Of Requests && Batch Size");
                    Console.WriteLine("\t- Cool Down Time && Batch Size. Requests will not stop until you stop it");
                    Console.WriteLine("\t- Number Of Requests. Test will complete when all the requests are completed");
                    Console.WriteLine("\t- Duration. Test will complete once the duration expires");
                    throw new ArgumentException();
                }
            });
            #endregion

            _requestCountOption = requestCountOption;
            _httpMethodOption = httpMethodOption;
            _httpversionOption = httpversionOption;
            _urlOption = urlOption;
            _headerOption = headerOption;
            _payloadOption = payloadOption;
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
                LPSRequest = new LPSHttpRequest.SetupCommand()
                {
                    HttpMethod = bindingContext.ParseResult.GetValueForOption(_httpMethodOption),
                    Httpversion = bindingContext.ParseResult.GetValueForOption(_httpversionOption),
                    URL = bindingContext.ParseResult.GetValueForOption(_urlOption),
                    Payload = !string.IsNullOrEmpty(bindingContext.ParseResult.GetValueForOption(_payloadOption)) ?
                    InputPayloadService.Parse(bindingContext.ParseResult.GetValueForOption(_payloadOption)) : string.Empty,
                    HttpHeaders = InputHeaderService.Parse(bindingContext.ParseResult.GetValueForOption(_headerOption))
                },
            };
    }
}
