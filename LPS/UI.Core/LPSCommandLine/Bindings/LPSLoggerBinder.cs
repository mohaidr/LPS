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

namespace LPS.UI.Core.LPSCommandLine.Bindings
{
    public class LPSLoggerBinder : BinderBase<LPSFileLoggerOptions>
    {


        private Option<string> _logFilePathOption;
        private Option<bool?> _enableConsoleLoggingOption;
        private Option<bool?> _disableConsoleErrorLoggingOption;
        private Option<bool?> _disableFileLoggingOption;
        private Option<LPSLoggingLevel?> _loggingLevelOption;
        private Option<LPSLoggingLevel?> _consoleLoggingLevelOption;


        public LPSLoggerBinder(
            Option<string> logFilePathOption = null,
            Option<bool?> disableFileLoggingOption = null,
            Option<bool?> enableConsoleLoggingOption = null,
            Option<bool?> disableConsoleErrorLoggingOption = null,
            Option<LPSLoggingLevel?> loggingLevelOption = null,
            Option<LPSLoggingLevel?> consoleLoggingLevelOption = null)
        {
            _logFilePathOption = logFilePathOption ?? LPSCommandLineOptions.LPSLoggerCommandOptions.LogFilePathOption;
            _enableConsoleLoggingOption = enableConsoleLoggingOption ?? LPSCommandLineOptions.LPSLoggerCommandOptions.EnableConsoleLoggingOption;
            _disableConsoleErrorLoggingOption = disableConsoleErrorLoggingOption ?? LPSCommandLineOptions.LPSLoggerCommandOptions.DisableConsoleErrorLoggingOption;
            _disableFileLoggingOption = disableFileLoggingOption ?? LPSCommandLineOptions.LPSLoggerCommandOptions.DisableFileLoggingOption;
            _loggingLevelOption = loggingLevelOption ?? LPSCommandLineOptions.LPSLoggerCommandOptions.LoggingLevelOption;
            _consoleLoggingLevelOption = consoleLoggingLevelOption ?? LPSCommandLineOptions.LPSLoggerCommandOptions.ConsoleLoggingLevelOption;
        }

        protected override LPSFileLoggerOptions GetBoundValue(BindingContext bindingContext) =>
            new LPSFileLoggerOptions
            {
                LogFilePath = bindingContext.ParseResult.GetValueForOption(_logFilePathOption),
                EnableConsoleLogging = bindingContext.ParseResult.GetValueForOption(_enableConsoleLoggingOption),
                DisableConsoleErrorLogging = bindingContext.ParseResult.GetValueForOption(_disableConsoleErrorLoggingOption),
                DisableFileLogging = bindingContext.ParseResult.GetValueForOption(_disableFileLoggingOption),
                LoggingLevel = bindingContext.ParseResult.GetValueForOption(_loggingLevelOption),
                ConsoleLogingLevel = bindingContext.ParseResult.GetValueForOption(_consoleLoggingLevelOption),
            };
    }
}
