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

namespace LPS.UI.Core.UI.Build.Services
{
    public class LPSRequestWrapperBinder : BinderBase<LPSRequestWrapper.SetupCommand>
    {
        private readonly Option<string> _nameOption;
        private readonly Option<int> _repeatOption;
        private readonly Option<string> _httpMethodOption;
        private readonly Option<string> _httpversionOption;
        private readonly Option<int> _timeoutOption;
        private readonly Option<string> _urlOption;
        private readonly Option<IList<string>> _headerOption;
        private readonly Option<string> _payloadOption;

        public LPSRequestWrapperBinder(Option<string> nameOption, Option<int> repeatOption,
            Option<string> httpMethodOption, Option<string> httpversionOption, Option<int> timeoutOption, Option<string> urlOption,
            Option<IList<string>> headerOption, Option<string> payloadOption)
        {
            _nameOption = nameOption;
            _repeatOption = repeatOption;
            _httpMethodOption = httpMethodOption;
            _httpversionOption = httpversionOption;
            _urlOption = urlOption;
            _headerOption = headerOption;
            _payloadOption = payloadOption;
            _timeoutOption = timeoutOption;
        }

        protected override LPSRequestWrapper.SetupCommand GetBoundValue(BindingContext bindingContext) =>
            new LPSRequestWrapper.SetupCommand
            {
                Name = bindingContext.ParseResult.GetValueForOption(_nameOption),
                NumberofAsyncRepeats = bindingContext.ParseResult.GetValueForOption(_repeatOption),
                LPSRequest = new LPSRequest.SetupCommand()
                {
                    HttpMethod = bindingContext.ParseResult.GetValueForOption(_httpMethodOption),
                    Httpversion = bindingContext.ParseResult.GetValueForOption(_httpversionOption),
                    TimeOut = bindingContext.ParseResult.GetValueForOption(_timeoutOption),
                    URL = bindingContext.ParseResult.GetValueForOption(_urlOption),
                    Payload = bindingContext.ParseResult.GetValueForOption(_payloadOption)!=null ? InputPayloadService.ReadFromFile(bindingContext.ParseResult.GetValueForOption(_payloadOption)): string.Empty,
                    HttpHeaders = InputHeaderService.Parse(bindingContext.ParseResult.GetValueForOption(_headerOption))
                },
            };
    }

    public class CommandLineOptions
    {
        static CommandLineOptions()
        {
            TestNameOption.AddAlias("-tn");
            NameOption.AddAlias("-n");
            RepeatOption.AddAlias("-r");
            HttpMethodOption.AddAlias("-m");
            HttpversionOption.AddAlias("-v");
            TimeoutOption.AddAlias("-t");
            UrlOption.AddAlias("-u");
            HeaderOption.AddAlias("-h");
            PayloadOption.AddAlias("-p");
        }
        public static Option<string> TestNameOption = new Option<string>("--testname", "Test name") { IsRequired = true, Arity = ArgumentArity.ExactlyOne };
        public static Option<string> NameOption = new Option<string>("--name", "The name of the request") { IsRequired = true };
        public static Option<int> RepeatOption = new Option<int>("--repeat", "The number of requests") { IsRequired = true, };
        public static Option<string> HttpMethodOption = new Option<string>("--method", "HTTP method") { IsRequired = true };
        public static Option<string> HttpversionOption = new Option<string>("--version", ()=> "1.1", "HTTP version") { IsRequired = false };
        public static Option<int> TimeoutOption = new Option<int>("--timeout", () => 4, "Timeout") { IsRequired = false };
        public static Option<string> UrlOption = new Option<string>("--url", "URL") { IsRequired = true };
        public static Option<IList<string>> HeaderOption = new Option<IList<string>>("--header", "Header") { IsRequired = false, AllowMultipleArgumentsPerToken = true, };
        public static Option<string> PayloadOption = new Option<string>("--payload", "Request Payload, can be a path to a file or inline text") { IsRequired = false, };
    }
}

