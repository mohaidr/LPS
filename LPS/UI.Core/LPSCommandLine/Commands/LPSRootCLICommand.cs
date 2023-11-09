using LPS.Domain;
using System;
using System.Collections.Generic;
using System.Linq;
using System.CommandLine;
using System.IO;
using Newtonsoft.Json;
using System.CommandLine.Binding;
using LPS.Domain.Common;
using LPS.UI.Common;
using System.Threading;
using LPS.UI.Common.Options;
using Microsoft.Extensions.Options;

namespace LPS.UI.Core.LPSCommandLine.Commands
{
    public class LPSRootCLICommand : ILPSCLICommand
    {
        private string[] _args;
        ILPSLogger _logger;
        LPSTestPlan.SetupCommand _command;
        ILPSClientManager<LPSHttpRequestProfile, ILPSClientService<LPSHttpRequestProfile>> _httpClientManager;
        ILPSClientConfiguration<LPSHttpRequestProfile> _config;
        IRuntimeOperationIdProvider _runtimeOperationIdProvider;
        ILPSWatchdog _watchdog;
        Command _lpsRootCliCommand;
        LPSCreateCLICommand _lpsCreateCliCommand;
        LPSAddCLICommand _lpsAddCliCommand;
        LPSRunCLICommand _lpsRunCliCommand;
        LPSLoggerCLICommand _lpsLoggerCliCommand;
        LPSAppSettingsWritableOptions _appSettings;
        public LPSRootCLICommand(ILPSLogger logger,
            ILPSClientManager<LPSHttpRequestProfile,
            ILPSClientService<LPSHttpRequestProfile>> httpClientManager,
            ILPSClientConfiguration<LPSHttpRequestProfile> config,
            ILPSWatchdog watchdog,
            IRuntimeOperationIdProvider runtimeOperationIdProvider,
            string[] args,
            LPSAppSettingsWritableOptions appSettings,
            LPSTestPlan.SetupCommand command)
        {
            _logger = logger;
            _command = command;
            _args = args;
            _config = config;
            _httpClientManager = httpClientManager;
            _watchdog = watchdog;
            _runtimeOperationIdProvider = runtimeOperationIdProvider;
            _appSettings = appSettings;
            Setup();
        }
        public LPSTestPlan.SetupCommand Command { get { return _command; } }

        private void Setup()
        {
            _lpsRootCliCommand = new Command("lps", "Load, Performance and Stress Testing Command Tool.");
            _lpsCreateCliCommand = new LPSCreateCLICommand(_lpsRootCliCommand, _command, _args);
            _lpsAddCliCommand = new LPSAddCLICommand(_lpsRootCliCommand, _command, _args);
            _lpsRunCliCommand = new LPSRunCLICommand(_lpsRootCliCommand, _command, _logger, _httpClientManager, _config, _runtimeOperationIdProvider, _watchdog, _args);
            _lpsLoggerCliCommand = new LPSLoggerCLICommand(_lpsRootCliCommand, _appSettings.LPSFileLoggerOptions, _args);
        }

        public void Execute(CancellationToken cancellationToken)
        {
            _lpsCreateCliCommand.Execute(cancellationToken);
            _lpsAddCliCommand.Execute(cancellationToken);
            _lpsRunCliCommand.Execute(cancellationToken);
            _lpsLoggerCliCommand.Execute(cancellationToken);
        }
    }
}
