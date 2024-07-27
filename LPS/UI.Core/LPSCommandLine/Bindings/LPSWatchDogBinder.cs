using LPS.Domain;
using LPS.UI.Core.UI.Build.Services;
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

namespace LPS.UI.Core.LPSCommandLine.Bindings
{
    public class LPSWatchdogBinder : BinderBase<LPSWatchdogOptions>
    {
        private Option<int?> _maxMemoryMB;
        private Option<int?> _maxCPUPercentage;
        private Option<int?> _coolDownMemoryMB;
        private Option<int?> _coolDownCPUPercentage;
        private Option<int?> _maxConcurrentConnectionsCountPerHostName;
        private Option<int?> _coolDownConcurrentConnectionsCountPerHostName;
        private Option<int?> _coolDownRetryTimeInSeconds;
        private Option<SuspensionMode?> _suspensionMode;



        public LPSWatchdogBinder(
            Option<int?>? maxMemoryMB = null,
            Option<int?>? maxCPUPercentage = null,
            Option<int?>? coolDownMemoryMB = null,
            Option<int?>? coolDownCPUPercentage = null,
            Option<int?>? maxConcurrentConnectionsCountPerHostName = null,
            Option<int?>? coolDownConcurrentConnectionsCountPerHostName = null,
            Option<int?>? coolDownRetryTimeInSeconds = null,
            Option<SuspensionMode?>? suspensionMode = null)
        {
            _maxMemoryMB = maxMemoryMB ?? LPSCommandLineOptions.LPSWatchdogCommandOptions.MaxMemoryMB;
            _maxCPUPercentage = maxCPUPercentage ?? LPSCommandLineOptions.LPSWatchdogCommandOptions.MaxCPUPercentage;
            _coolDownMemoryMB = coolDownMemoryMB ?? LPSCommandLineOptions.LPSWatchdogCommandOptions.CoolDownMemoryMB;
            _coolDownCPUPercentage = coolDownCPUPercentage ?? LPSCommandLineOptions.LPSWatchdogCommandOptions.CoolDownCPUPercentage;
            _maxConcurrentConnectionsCountPerHostName = maxConcurrentConnectionsCountPerHostName ?? LPSCommandLineOptions.LPSWatchdogCommandOptions.MaxConcurrentConnectionsCountPerHostName;
            _coolDownConcurrentConnectionsCountPerHostName = coolDownConcurrentConnectionsCountPerHostName ?? LPSCommandLineOptions.LPSWatchdogCommandOptions.CoolDownConcurrentConnectionsCountPerHostName;
            _coolDownRetryTimeInSeconds = coolDownRetryTimeInSeconds ?? LPSCommandLineOptions.LPSWatchdogCommandOptions.CoolDownRetryTimeInSeconds;
            _suspensionMode = suspensionMode ?? LPSCommandLineOptions.LPSWatchdogCommandOptions.SuspensionMode;
        }

        protected override LPSWatchdogOptions GetBoundValue(BindingContext bindingContext) =>
            new LPSWatchdogOptions
            {
                MaxMemoryMB = bindingContext.ParseResult.GetValueForOption(_maxMemoryMB),
                MaxCPUPercentage = bindingContext.ParseResult.GetValueForOption(_maxCPUPercentage),
                MaxConcurrentConnectionsCountPerHostName = bindingContext.ParseResult.GetValueForOption(_maxConcurrentConnectionsCountPerHostName),
                CoolDownMemoryMB = bindingContext.ParseResult.GetValueForOption(_coolDownMemoryMB),
                CoolDownCPUPercentage = bindingContext.ParseResult.GetValueForOption(_coolDownCPUPercentage),
                CoolDownConcurrentConnectionsCountPerHostName = bindingContext.ParseResult.GetValueForOption(_coolDownConcurrentConnectionsCountPerHostName),
                CoolDownRetryTimeInSeconds = bindingContext.ParseResult.GetValueForOption(_coolDownRetryTimeInSeconds),
                SuspensionMode = bindingContext.ParseResult.GetValueForOption(_suspensionMode),
            };
    }
}
