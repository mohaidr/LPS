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
using LPS.Domain.Common;

namespace LPS.UI.Core.LPSCommandLine.Bindings
{
    public class LPSLoggerBinder : BinderBase<LPSFileLoggerOptions>
    {


        public static Option<string> _logFilePathOption;
        public static Option<bool> _enableConsoleLoggingOption;
        public static Option<bool> _disableConsoleErrorLoggingOption;
        public static Option<bool> _disableFileLoggingOption;
        public static Option<LPSLoggingLevel> _loggingLevelOption;
        public static Option<LPSLoggingLevel> _consoleLoggingLevelOption;


        public LPSLoggerBinder(Option<string> logFilePathOption,
        Option<bool> disableFileLoggingOption,
        Option<bool> enableConsoleLoggingOption,
        Option<bool> disableConsoleErrorLoggingOption,
        Option<LPSLoggingLevel> loggingLevelOption,
        Option<LPSLoggingLevel> consoleLoggingLevelOption)
        {
            _logFilePathOption= logFilePathOption;
            _enableConsoleLoggingOption = enableConsoleLoggingOption;
            _disableConsoleErrorLoggingOption = disableConsoleErrorLoggingOption;
            _disableFileLoggingOption = disableFileLoggingOption;
            _loggingLevelOption = loggingLevelOption;
            _consoleLoggingLevelOption = consoleLoggingLevelOption;
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
