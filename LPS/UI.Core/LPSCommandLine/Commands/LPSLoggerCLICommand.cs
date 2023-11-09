using LPS.UI.Common;
using LPS.UI.Common.Options;
using LPS.UI.Core.LPSCommandLine.Bindings;
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
        public LPSLoggerCLICommand(Command rootLpsCliCommand, IWritableOptions<LPSFileLoggerOptions> loggerOptions, string[] args) 
        {
            _rootLpsCliCommand = rootLpsCliCommand;
            _args = args;
            _loggerOptions = loggerOptions;
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
                _loggerOptions.Update(option => {
                    option.LogFilePath = updateLoggerOptions.LogFilePath;
                    option.DisableFileLogging = updateLoggerOptions.DisableFileLogging;
                    option.LoggingLevel = updateLoggerOptions.LoggingLevel;
                    option.ConsoleLogingLevel = updateLoggerOptions.ConsoleLogingLevel;
                    option.EnableConsoleLogging = updateLoggerOptions.EnableConsoleLogging;
                    option.DisableConsoleErrorLogging = updateLoggerOptions.DisableConsoleErrorLogging;
                });

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
