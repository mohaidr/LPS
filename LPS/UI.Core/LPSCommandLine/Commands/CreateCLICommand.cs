using LPS.Domain;
using LPS.Domain.Common.Interfaces;
using LPS.Domain.Domain.Common.Interfaces;
using LPS.Infrastructure.Common;
using LPS.Infrastructure.Monitoring;
using LPS.UI.Common;
using LPS.UI.Common.Options;
using Microsoft.Extensions.Options;
using System;
using System.CommandLine;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using YamlDotNet.Serialization.NamingConventions;
using YamlDotNet.Serialization;
using LPS.UI.Core.Services;
using static LPS.UI.Core.LPSCommandLine.CommandLineOptions;

namespace LPS.UI.Core.LPSCommandLine.Commands
{
    internal class CreateCLICommand : ICLICommand
    {
        readonly Command _rootCliCommand;
        private string[] _args;
        readonly ILogger _logger;
        readonly IClientManager<HttpRequestProfile, HttpResponse, IClientService<HttpRequestProfile, HttpResponse>> _httpClientManager;
        readonly IClientConfiguration<HttpRequestProfile> _config;
        readonly IRuntimeOperationIdProvider _runtimeOperationIdProvider;
        readonly IWatchdog _watchdog;
        Command _createCommand;
        readonly IMetricsDataMonitor _lPSMonitoringEnroller;
        readonly ICommandStatusMonitor<IAsyncCommand<HttpIteration>, HttpIteration> _httpIterationExecutionCommandStatusMonitor;
        readonly IOptions<DashboardConfigurationOptions> _dashboardConfig;
        readonly CancellationTokenSource _cts;

        internal CreateCLICommand(
            Command rootCLICommandLine,
            ILogger logger,
            IClientManager<HttpRequestProfile, HttpResponse, IClientService<HttpRequestProfile, HttpResponse>> httpClientManager,
            IClientConfiguration<HttpRequestProfile> config,
            IRuntimeOperationIdProvider runtimeOperationIdProvider,
            IWatchdog watchdog,
            ICommandStatusMonitor<IAsyncCommand<HttpIteration>, HttpIteration> httpIterationExecutionCommandStatusMonitor,
            IMetricsDataMonitor lPSMonitoringEnroller,
            IOptions<DashboardConfigurationOptions> dashboardConfig,
            CancellationTokenSource cts,
            string[] args)
        {
            _rootCliCommand = rootCLICommandLine;
            _args = args;
            _logger = logger;
            _httpClientManager = httpClientManager;
            _config = config;
            _runtimeOperationIdProvider = runtimeOperationIdProvider;
            _watchdog = watchdog;
            _httpIterationExecutionCommandStatusMonitor = httpIterationExecutionCommandStatusMonitor;
            _lPSMonitoringEnroller = lPSMonitoringEnroller;
            _dashboardConfig = dashboardConfig;
            _cts = cts;
            Setup();
        }

        private void Setup()
        {
            _createCommand = new Command("create", "Run existing test");

            // Add the positional argument directly to _runCommand
            _createCommand.AddArgument(LPSCreateCommandOptions.ConfigFileArgument);
            CommandLineOptions.AddOptionsToCommand(_createCommand, typeof(LPSCreateCommandOptions));

            _rootCliCommand.AddCommand(_createCommand);
        }

        public async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            _createCommand.SetHandler((string configFile, string name) =>
            {
                try
                {
                    Plan.SetupCommand setupCommand;

                    if (File.Exists(configFile))
                    {
                        _logger.Log(_runtimeOperationIdProvider.OperationId, $"{configFile} File exists, fetching configuration.", LPSLoggingLevel.Information);
                        setupCommand = ConfigurationService.FetchConfiguration(configFile);
                        setupCommand.Name = name;
                    }
                    else
                    {
                        _logger.Log(_runtimeOperationIdProvider.OperationId, $"{configFile} File does not exist, creating new setup command.", LPSLoggingLevel.Information);

                        setupCommand = new Plan.SetupCommand { Name = name };
                    }

                    ConfigurationService.SaveConfiguration(configFile, setupCommand);

                    _logger.Log(_runtimeOperationIdProvider.OperationId, $"Configuration file '{configFile}' updated with plan name '{name}'.", LPSLoggingLevel.Information);

                }
                catch (Exception ex)
                {
                    _logger.Log(_runtimeOperationIdProvider.OperationId, ex.Message, LPSLoggingLevel.Error);
                }
            },
            LPSCreateCommandOptions.ConfigFileArgument,
            LPSCreateCommandOptions.PlanNameOption);

            await _rootCliCommand.InvokeAsync(_args);
        }
    }
}
