using LPS.Extensions;
using LPS.Domain;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.CommandLine;
using System.CommandLine.Binding;
using LPS.UI.Core.UI.Build.Services;
using System.Net.Http;
using System.Net;
using System.Reflection.PortableExecutable;
using System.Threading;
using System.Xml.Linq;
using System.CommandLine.Parsing;
using System.ComponentModel.DataAnnotations;
using static LPS.Domain.LPSHttpTestCase;

namespace LPS.UI.Core.UI.Build.Services
{
    public class LPSTestCaseBinder : BinderBase<LPSHttpTestCase.SetupCommand>
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

        public LPSTestCaseBinder(Option<string> nameOption, Option<int?> requestCountOption,
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
            _batchSize= batchSizem;
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
                int?  coolDownTime = result.Parent.Children.OfType<OptionResult>()
                    .FirstOrDefault(r => r.Symbol == _coolDownTime)?.GetValueOrDefault<int?>();

                bool invalidIterationModeCombination = false;
                if (mode.Value == IterationMode.DCB)
                {
                    if (!duration.HasValue || duration.Value <= 0
                        || !coolDownTime.HasValue || coolDownTime.Value <= 0
                        || !batchSize.HasValue || batchSize.Value <= 0
                        || requestCount.HasValue)
                    {
                        invalidIterationModeCombination = true;
                    }
                }
                else if (mode.Value == IterationMode.CRB)
                {
                    if (!coolDownTime.HasValue || coolDownTime.Value <= 0
                        || !requestCount.HasValue || requestCount.Value <= 0
                        || !batchSize.HasValue || batchSize.Value <= 0
                        || duration.HasValue)
                    {
                        invalidIterationModeCombination = true;
                    }
                }
                else if (mode.Value == IterationMode.CB)
                {
                    if (!coolDownTime.HasValue || coolDownTime.Value <= 0
                        || !batchSize.HasValue || batchSize.Value <= 0
                        || duration.HasValue
                        || requestCount.HasValue)
                    {
                        invalidIterationModeCombination = true;

                    }
                }
                else if (mode.Value == IterationMode.R)
                {
                    if (!requestCount.HasValue || requestCount.Value <= 0
                        || duration.HasValue
                        || batchSize.HasValue
                        || coolDownTime.HasValue)
                    {
                     
                        invalidIterationModeCombination = true;
                    }

                }
                else if (mode.Value == IterationMode.D)
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
                    Payload = bindingContext.ParseResult.GetValueForOption(_payloadOption) != null ? InputPayloadService.ReadFromFile(bindingContext.ParseResult.GetValueForOption(_payloadOption)) : string.Empty,
                    HttpHeaders = InputHeaderService.Parse(bindingContext.ParseResult.GetValueForOption(_headerOption))
                },
            };
    }

    public class CommandLineOptions
    {
        static CommandLineOptions()
        {
            TestNameOption.AddAlias("-tn");
            NumberOfClients.AddAlias("-nc");
            RampupPeriod.AddAlias("-rp");
            MaxConnectionsPerServer.AddAlias("-mcps");
            PoolConnectionLifeTime.AddAlias("-pclt");
            PoolConnectionIdelTimeout.AddAlias("-pcit");
            DelayClientCreation.AddAlias("dcc");
            ClientTimeoutOption.AddAlias("-cto");
            CaseNameOption.AddAlias("-cn");
            RequestCountOption.AddAlias("-rc");
            Duratiion.AddAlias("-d");
            BatchSize.AddAlias("-bs");
            CoolDownTime.AddAlias("-cdt");
            HttpMethodOption.AddAlias("-hm");
            HttpversionOption.AddAlias("-hv");
            UrlOption.AddAlias("-u");
            HeaderOption.AddAlias("-h");
            PayloadOption.AddAlias("-p");
            IterationModeOption.AddAlias("-im");

        }
        public static Option<string> TestNameOption = new Option<string>("--testname", "Test name") { IsRequired = true, Arity = ArgumentArity.ExactlyOne };
        public static Option<int> NumberOfClients = new Option<int>("--numberOfClients", "") { IsRequired = true, };
        public static Option<int> RampupPeriod = new Option<int>("--rampupPeriod", "") { IsRequired = false, };
        public static Option<int> MaxConnectionsPerServer = new Option<int>("--maxConnectionsPerServer", () => 1000, "") { IsRequired = false, };
        public static Option<int> PoolConnectionLifeTime = new Option<int>("--poolConnectionLifeTime", () => 25, "") { IsRequired = false, };
        public static Option<int> PoolConnectionIdelTimeout = new Option<int>("--poolConnectionIdelTimeout", () => 5, "") { IsRequired = false, };
        public static Option<int> ClientTimeoutOption = new Option<int>("--clientTimeout", () => 240, "Timeout") { IsRequired = false };
        public static Option<bool> DelayClientCreation = new Option<bool>("--delayClientCreation", () => false, "Delay Client Creation Until is Needed") { IsRequired = false };
        public static Option<string> CaseNameOption = new Option<string>("--caseName", "The name of the test case") { IsRequired = true };
        public static Option<LPSHttpTestCase.IterationMode> IterationModeOption = new Option<LPSHttpTestCase.IterationMode>("--iterationMode", "The Test Iteration Mode") { IsRequired = false, };
        public static Option<int?> RequestCountOption = new Option<int?>("--requestCount", "The number of requests") { IsRequired = false, };
        public static Option<int?> Duratiion = new Option<int?>("--duration", "Test time in seconds") { IsRequired = false, };
        public static Option<int?> CoolDownTime = new Option<int?>("--coolDownTime", "The time to cooldown during the test") { IsRequired = false, };
        public static Option<int?> BatchSize = new Option<int?>("--batchsize", "The number of requests to be sent in a batch") { IsRequired = false, };
        public static Option<string> HttpMethodOption = new Option<string>("--method", "HTTP method") { IsRequired = true };
        public static Option<string> HttpversionOption = new Option<string>("--version", () => "1.1", "HTTP version") { IsRequired = false };
        public static Option<string> UrlOption = new Option<string>("--url", "URL") { IsRequired = true };
        public static Option<IList<string>> HeaderOption = new Option<IList<string>>("--header", "Header") { IsRequired = false, AllowMultipleArgumentsPerToken = true, };
        public static Option<string> PayloadOption = new Option<string>("--payload", "Request Payload, can be a path to a file or inline text") { IsRequired = false, };
    }
}

