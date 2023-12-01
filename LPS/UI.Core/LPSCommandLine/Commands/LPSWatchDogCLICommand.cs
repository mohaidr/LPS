using FluentValidation;
using LPS.Domain.Common;
using LPS.Infrastructure.Logger;
using LPS.UI.Common;
using LPS.UI.Common.Extensions;
using LPS.UI.Common.Options;
using LPS.UI.Core.LPSCommandLine.Bindings;
using LPS.UI.Core.LPSValidators;
using LPS.UI.Core.UI.Build.Services;
using Microsoft.Extensions.Options;
using Microsoft.VisualBasic.FileIO;
using System;
using System.Collections.Generic;
using System.CommandLine;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace LPS.UI.Core.LPSCommandLine.Commands
{
    internal class LPSWatchDogCLICommand : ILPSCLICommand
    {
        private Command _rootLpsCliCommand;
        private Command _watchdogCommand;
        private string[] _args;
        IWritableOptions<LPSWatchdogOptions> _watchdogOptions;
        ILPSLogger _logger;
        IRuntimeOperationIdProvider _runtimeOperationIdProvider;
        public LPSWatchDogCLICommand(Command rootLpsCliCommand, ILPSLogger logger, IRuntimeOperationIdProvider runtimeOperationIdProvider, IWritableOptions<LPSWatchdogOptions> watchdogOptions, string[] args) 
        {
            _rootLpsCliCommand = rootLpsCliCommand;
            _args = args;
            _watchdogOptions = watchdogOptions;
            _logger = logger;
           _runtimeOperationIdProvider= runtimeOperationIdProvider;
            Setup();
        }
        private void Setup()
        {
            _watchdogCommand = new Command("watchdog", "Configure the LPS Watchdog");
            LPSCommandLineOptions.AddOptionsToCommand(_watchdogCommand, typeof(LPSCommandLineOptions.LPSWatchdogCommandOptions));
            _rootLpsCliCommand.AddCommand(_watchdogCommand);
        }

        public void Execute(CancellationToken cancellationToken)
        {

            _watchdogCommand.SetHandler((updatedWatchdogOptions) =>
            {
                var watchdogValidator = new LPSWatchdogValidator();
                LPSWatchdogOptions watchdoOptions = new LPSWatchdogOptions();
                //combine the configurations in the file with the provided ones by the command to validate the final object
                watchdoOptions.MaxMemoryMB = updatedWatchdogOptions.MaxMemoryMB ?? _watchdogOptions.Value.MaxMemoryMB;
                watchdoOptions.MaxCPUPercentage = updatedWatchdogOptions.MaxCPUPercentage ?? _watchdogOptions.Value.MaxCPUPercentage;
                watchdoOptions.MaxConcurrentConnectionsCountPerHostName = updatedWatchdogOptions.MaxConcurrentConnectionsCountPerHostName ?? _watchdogOptions.Value.MaxConcurrentConnectionsCountPerHostName;
                watchdoOptions.CoolDownMemoryMB = updatedWatchdogOptions.CoolDownMemoryMB ?? _watchdogOptions.Value.CoolDownMemoryMB;
                watchdoOptions.CoolDownCPUPercentage = updatedWatchdogOptions.CoolDownCPUPercentage ?? _watchdogOptions.Value.CoolDownCPUPercentage;
                watchdoOptions.CoolDownConcurrentConnectionsCountPerHostName = updatedWatchdogOptions.CoolDownConcurrentConnectionsCountPerHostName ?? _watchdogOptions.Value.CoolDownConcurrentConnectionsCountPerHostName;
                watchdoOptions.CoolDownRetryTimeInSeconds = updatedWatchdogOptions.CoolDownRetryTimeInSeconds ?? _watchdogOptions.Value.CoolDownRetryTimeInSeconds;
                watchdoOptions.SuspensionMode = updatedWatchdogOptions.SuspensionMode ?? _watchdogOptions.Value.SuspensionMode;

                var validationResults = watchdogValidator.Validate(watchdoOptions);
                if (!validationResults.IsValid)
                {
                    _logger.Log(_runtimeOperationIdProvider.OperationId, "You must update the below properties to have a valid watchdog configuration. Updating the LPSAppSettings:LPSWatchdogConfiguration section with the provided arguements will create an invalid watchdog configuration. You may run 'lps watchdog -h' to explore the options", LPSLoggingLevel.Warning);
                    validationResults.PrintValidationErrors();
                }
                else
                {
                    _watchdogOptions.Update(option =>
                    {
                        //do not do option = watchdoOptions;
                        option.MaxMemoryMB = watchdoOptions.MaxMemoryMB;
                        option.MaxCPUPercentage = watchdoOptions.MaxCPUPercentage;
                        option.MaxConcurrentConnectionsCountPerHostName = watchdoOptions.MaxConcurrentConnectionsCountPerHostName;
                        option.CoolDownMemoryMB = watchdoOptions.CoolDownMemoryMB;
                        option.CoolDownCPUPercentage = watchdoOptions.CoolDownCPUPercentage;
                        option.CoolDownConcurrentConnectionsCountPerHostName = watchdoOptions.CoolDownConcurrentConnectionsCountPerHostName;
                        option.CoolDownRetryTimeInSeconds = watchdoOptions.CoolDownRetryTimeInSeconds;
                        option.SuspensionMode = watchdoOptions.SuspensionMode;
                    });
                }
            }, new LPSWatchdogBinder());

            _rootLpsCliCommand.Invoke(_args);
        }
    }
}
