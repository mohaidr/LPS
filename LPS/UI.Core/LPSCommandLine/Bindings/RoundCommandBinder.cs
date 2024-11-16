using LPS.Domain;
using LPS.UI.Core.Build.Services;
using System;
using System.Collections.Generic;
using System.CommandLine.Binding;
using System.CommandLine;
using System.Linq;
using System.Text;
using System.CommandLine.Parsing;
using LPS.DTOs;

namespace LPS.UI.Core.LPSCommandLine.Bindings
{
    public class RoundCommandBinder(
        Option<string>? roundNameOption = null,
        Option<string>? baseUrlOption = null,
        Option<int>? startupDelayOption = null,
        Option<int>? numberOfClientsOption = null,
        Option<int>? arrivalDelayOption = null,
        Option<bool>? delayClientCreationOption = null,
        Option<bool?>? runInParallerOption = null) : BinderBase<RoundDto>
    {
        private readonly Option<string> _roundNameOption = roundNameOption ?? CommandLineOptions.LPSRoundCommandOptions.RoundNameOption;
        private readonly Option<string> _baseUrlOption = baseUrlOption ?? CommandLineOptions.LPSRoundCommandOptions.BaseUrlOption;
        private readonly Option<int> _startupDelayOption = startupDelayOption ?? CommandLineOptions.LPSRoundCommandOptions.StartupDelayOption;
        private readonly Option<int> _numberOfClientsOption = numberOfClientsOption ?? CommandLineOptions.LPSRoundCommandOptions.NumberOfClientsOption;
        private readonly Option<int> _arrivalDelayOption = arrivalDelayOption ?? CommandLineOptions.LPSRoundCommandOptions.ArrivalDelayOption;
        private readonly Option<bool> _delayClientCreationOption = delayClientCreationOption ?? CommandLineOptions.LPSRoundCommandOptions.DelayClientCreation;
        private readonly Option<bool?> _runInParallerOption = runInParallerOption ?? CommandLineOptions.LPSRoundCommandOptions.RunInParallel;

        protected override RoundDto GetBoundValue(BindingContext bindingContext) =>
            new()
            {
                Name = bindingContext.ParseResult.GetValueForOption(_roundNameOption),
                BaseUrl = bindingContext.ParseResult.GetValueForOption(_baseUrlOption),
                StartupDelay = bindingContext.ParseResult.GetValueForOption(_startupDelayOption),
                NumberOfClients = bindingContext.ParseResult.GetValueForOption(_numberOfClientsOption),
                ArrivalDelay = bindingContext.ParseResult.GetValueForOption(_arrivalDelayOption),
                DelayClientCreationUntilIsNeeded = bindingContext.ParseResult.GetValueForOption(_delayClientCreationOption),
                RunInParallel = bindingContext.ParseResult.GetValueForOption(_runInParallerOption),
            };
    }
}
