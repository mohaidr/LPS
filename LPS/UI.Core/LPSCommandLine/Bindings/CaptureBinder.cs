using LPS.Domain;
using LPS.UI.Core.Build.Services;
using System;
using System.Collections.Generic;
using System.CommandLine.Binding;
using System.CommandLine;
using System.Linq;
using System.Text;
using System.CommandLine.Parsing;
using LPS.UI.Common.Options;
using LPS.Domain.Common.Interfaces;
using LPS.Infrastructure.Watchdog;
using LPS.DTOs;
using static LPS.UI.Core.LPSCommandLine.CommandLineOptions;

namespace LPS.UI.Core.LPSCommandLine.Bindings
{
    public class CaptureBinder : BinderBase<CaptureHandlerDto>
    {
        private static Option<string>? _nameOption;
        private static Option<string>? _asOption;
        private static Option<string>? _regexOption;
        private static Option<bool>? _makeGlobal;



        public CaptureBinder(Option<string>? nameOption = null,
         Option<string>? valueOPtion = null,
        Option<string>? asOption = null,
         Option<string>? regexOption = null,
         Option<bool>? makeGlobal = null)
        {
            _nameOption = nameOption?? CaptureCommandOptions.NameOption;
            _asOption = asOption?? CaptureCommandOptions.AsOption;
            _regexOption = regexOption?? CaptureCommandOptions.RegexOption;
            _makeGlobal = makeGlobal ?? CaptureCommandOptions.MakeGlobal;
        }

#pragma warning disable CS8601 // Possible null reference assignment.
        protected override CaptureHandlerDto GetBoundValue(BindingContext bindingContext) =>
            new()
            {
                Name = bindingContext.ParseResult.GetValueForOption(_nameOption),
                As = bindingContext.ParseResult.GetValueForOption(_asOption),
                Regex = bindingContext.ParseResult.GetValueForOption(_regexOption),
                MakeGlobal = bindingContext.ParseResult.GetValueForOption(_makeGlobal),
            };
#pragma warning restore CS8601 // Possible null reference assignment.
    }
}
