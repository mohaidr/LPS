using LPS.Domain.Common;
using LPS.Infrastructure.Logger;
using LPS.UI.Common;
using LPS.UI.Common.Extensions;
using LPS.UI.Common.Options;
using LPS.UI.Core.LPSCommandLine.Bindings;
using LPS.UI.Core.UI.Build.Services;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.CommandLine;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace LPS.UI.Core.LPSCommandLine.Commands
{
    internal class LPSLoggerCLICommand : ILPSCLICommand
    {
        private Command _rootLpsCliCommand;
        private Command _loggerCommand;
        private string[] _args;
        IWritableOptions<LPSFileLoggerOptions> _loggerOptions;
        ILPSLogger _logger;
        IRuntimeOperationIdProvider _runtimeOperationIdProvider;
        public LPSLoggerCLICommand(Command rootLpsCliCommand, ILPSLogger logger, IRuntimeOperationIdProvider runtimeOperationIdProvider, IWritableOptions<LPSFileLoggerOptions> loggerOptions, string[] args) 
        {
            _rootLpsCliCommand = rootLpsCliCommand;
            _args = args;
            _loggerOptions = loggerOptions;
            _logger = logger;
           _runtimeOperationIdProvider= runtimeOperationIdProvider;
            Setup();
        }
        private void Setup()
        {
            _loggerCommand = new Command("logger", "Configure the LPS logger")
            {
                LPSCommandLineOptions.LogFilePathOption,
                LPSCommandLineOptions.DisableFileLoggingOption,
                LPSCommandLineOptions.EnableConsoleLoggingOption,
                LPSCommandLineOptions.DisableConsoleErrorLoggingOption,
                LPSCommandLineOptions.LoggingLevelOption,
                LPSCommandLineOptions.ConsoleLoggingLevelOption,
            };
            _rootLpsCliCommand.AddCommand(_loggerCommand);
        }

        public void Execute(CancellationToken cancellationToken)
        {

            _loggerCommand.SetHandler((updateLoggerOptions) =>
            {
                var loggerValidator = new LPSFileLoggerValidator();
                var validationResults = loggerValidator.Validate(updateLoggerOptions);
                if (!validationResults.IsValid)
                {
                    _logger.Log(_runtimeOperationIdProvider.OperationId, "Logger options are not valid, the default settings were applies", LPSLoggingLevel.Warning);
                    validationResults.PrintValidationErrors();
                }
                else
                {
                    _loggerOptions.Update(option =>
                    {
                        option.LogFilePath = updateLoggerOptions.LogFilePath;
                        option.DisableFileLogging = updateLoggerOptions.DisableFileLogging;
                        option.LoggingLevel = updateLoggerOptions.LoggingLevel;
                        option.ConsoleLogingLevel = updateLoggerOptions.ConsoleLogingLevel;
                        option.EnableConsoleLogging = updateLoggerOptions.EnableConsoleLogging;
                        option.DisableConsoleErrorLogging = updateLoggerOptions.DisableConsoleErrorLogging;
                    });
                }
            }, new LPSLoggerBinder(
                LPSCommandLineOptions.LogFilePathOption,
                LPSCommandLineOptions.DisableFileLoggingOption,
                LPSCommandLineOptions.EnableConsoleLoggingOption,
                LPSCommandLineOptions.DisableConsoleErrorLoggingOption,
                LPSCommandLineOptions.LoggingLevelOption,
                LPSCommandLineOptions.ConsoleLoggingLevelOption));

            _rootLpsCliCommand.Invoke(_args);
        }
    }
}
