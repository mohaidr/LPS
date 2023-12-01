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
            _loggerCommand = new Command("logger", "Configure the LPS logger");
            LPSCommandLineOptions.AddOptionsToCommand(_loggerCommand, typeof(LPSCommandLineOptions.LPSLoggerCommandOptions));
            _rootLpsCliCommand.AddCommand(_loggerCommand);
        }

        public void Execute(CancellationToken cancellationToken)
        {

            _loggerCommand.SetHandler((updateLoggerOptions) =>
            {
                var loggerValidator = new LPSFileLoggerValidator();
                LPSFileLoggerOptions fileLoggerOptions = new LPSFileLoggerOptions();
                // Combine the provided logger options by the command and what in the config section to validate the final object
                fileLoggerOptions.LogFilePath = !string.IsNullOrWhiteSpace(updateLoggerOptions.LogFilePath) ? updateLoggerOptions.LogFilePath: _loggerOptions.Value.LogFilePath;
                fileLoggerOptions.DisableFileLogging = updateLoggerOptions.DisableFileLogging?? _loggerOptions.Value.DisableFileLogging ;
                fileLoggerOptions.LoggingLevel = updateLoggerOptions.LoggingLevel?? _loggerOptions.Value.LoggingLevel;
                fileLoggerOptions.ConsoleLogingLevel = updateLoggerOptions.ConsoleLogingLevel ?? _loggerOptions.Value.ConsoleLogingLevel;
                fileLoggerOptions.EnableConsoleLogging = updateLoggerOptions.EnableConsoleLogging?? _loggerOptions.Value.EnableConsoleLogging;
                fileLoggerOptions.DisableConsoleErrorLogging = updateLoggerOptions.DisableConsoleErrorLogging ?? _loggerOptions.Value.DisableConsoleErrorLogging;
                var validationResults = loggerValidator.Validate(fileLoggerOptions);

                if (!validationResults.IsValid)
                {
                    _logger.Log(_runtimeOperationIdProvider.OperationId, "You must update the below properties to have a valid logger configuration. Updating the LPSAppSettings:LPSFileLoggerConfiguration section with the provided arguements will create an invalid logger configuration. You may run 'lps logger -h' to explore the options", LPSLoggingLevel.Warning);
                    validationResults.PrintValidationErrors();
                }
                else
                {
                    _loggerOptions.Update(option =>
                    {
                        option.LogFilePath = fileLoggerOptions.LogFilePath;
                        option.DisableFileLogging = fileLoggerOptions.DisableFileLogging;
                        option.LoggingLevel = fileLoggerOptions.LoggingLevel;
                        option.ConsoleLogingLevel = fileLoggerOptions.ConsoleLogingLevel;
                        option.EnableConsoleLogging = fileLoggerOptions.EnableConsoleLogging;
                        option.DisableConsoleErrorLogging = fileLoggerOptions.DisableConsoleErrorLogging;
                    });
                }
            }, new LPSLoggerBinder());

            _rootLpsCliCommand.Invoke(_args);
        }
    }
}
