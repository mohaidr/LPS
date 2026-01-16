using LPS.Domain.Common.Interfaces;
using LPS.Infrastructure.Logger;
using LPS.UI.Common;
using LPS.UI.Common.Extensions;
using LPS.UI.Common.Options;
using LPS.UI.Core.LPSCommandLine.Bindings;
using LPS.UI.Core.LPSValidators;
using LPS.UI.Core.Build.Services;
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
    internal class DashboardCliCommand : ICliCommand
    {
        private Command _rootLpsCliCommand;
        private Command _dashboardCliCommand;
        public Command Command => _dashboardCliCommand;
        IWritableOptions<DashboardConfigurationOptions> _dashboardOptions;
        ILogger _logger;
        IRuntimeOperationIdProvider _runtimeOperationIdProvider;

        public DashboardCliCommand(Command rootLpsCliCommand, ILogger logger, IRuntimeOperationIdProvider runtimeOperationIdProvider, IWritableOptions<DashboardConfigurationOptions> dashboardOptions)
        {
            _rootLpsCliCommand = rootLpsCliCommand;
            _dashboardOptions = dashboardOptions;
            _logger = logger;
            _runtimeOperationIdProvider = runtimeOperationIdProvider;
            Setup();
        }

        private void Setup()
        {
            _dashboardCliCommand = new Command("dashboard", "Configure the LPS dashboard");
            CommandLineOptions.AddOptionsToCommand(_dashboardCliCommand, typeof(CommandLineOptions.LPSDashboardCommandOptions));
            _rootLpsCliCommand.AddCommand(_dashboardCliCommand);
        }

        public void SetHandler(CancellationToken cancellationToken)
        {
            _dashboardCliCommand.SetHandler((updateDashboardOptions) =>
            {
                var dashboardValidator = new DashboardConfigurationValidator();
                DashboardConfigurationOptions dashboardConfigurationOptions = new()
                {
                    // Combine the provided dashboard options by the command and what in the config section to validate the final object
                    BuiltInDashboard = updateDashboardOptions.BuiltInDashboard ?? _dashboardOptions.Value.BuiltInDashboard,
                    Port = updateDashboardOptions.Port ?? _dashboardOptions.Value.Port,
                    RefreshRate = updateDashboardOptions.RefreshRate ?? _dashboardOptions.Value.RefreshRate
                };

                var validationResults = dashboardValidator.Validate(dashboardConfigurationOptions);

                if (!validationResults.IsValid)
                {
                    _logger.Log(_runtimeOperationIdProvider.OperationId, "You must update the below properties to have a valid dashboard configuration. Updating the LPSAppSettings:Dashboard section with the provided arguments will create an invalid dashboard configuration. You may run 'lps dashboard -h' to explore the options", LPSLoggingLevel.Warning);
                    validationResults.PrintValidationErrors();
                }
                else
                {
                    _dashboardOptions.Update(option =>
                    {
                        option.BuiltInDashboard = dashboardConfigurationOptions.BuiltInDashboard;
                        option.Port = dashboardConfigurationOptions.Port;
                        option.RefreshRate = dashboardConfigurationOptions.RefreshRate;
                    });
                }
            }, new DashboardBinder());
        }
    }
}