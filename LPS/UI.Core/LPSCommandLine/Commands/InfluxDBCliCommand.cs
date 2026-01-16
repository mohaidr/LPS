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
    internal class InfluxDBCliCommand : ICliCommand
    {
        private Command _rootLpsCliCommand;
        private Command _influxDbCliCommand;
        public Command Command => _influxDbCliCommand;
        IWritableOptions<LPS.UI.Common.Options.InfluxDBOptions> _influxDbOptions;
        ILogger _logger;
        IRuntimeOperationIdProvider _runtimeOperationIdProvider;

        public InfluxDBCliCommand(Command rootLpsCliCommand, ILogger logger, IRuntimeOperationIdProvider runtimeOperationIdProvider, IWritableOptions<LPS.UI.Common.Options.InfluxDBOptions> influxDbOptions)
        {
            _rootLpsCliCommand = rootLpsCliCommand;
            _influxDbOptions = influxDbOptions;
            _logger = logger;
            _runtimeOperationIdProvider = runtimeOperationIdProvider;
            Setup();
        }

        private void Setup()
        {
            _influxDbCliCommand = new Command("influxdb", "Configure the InfluxDB integration");
            CommandLineOptions.AddOptionsToCommand(_influxDbCliCommand, typeof(CommandLineOptions.LPSInfluxDBCommandOptions));
            _rootLpsCliCommand.AddCommand(_influxDbCliCommand);
        }

        public void SetHandler(CancellationToken cancellationToken)
        {
            _influxDbCliCommand.SetHandler((updateInfluxDbOptions) =>
            {
                var influxDbValidator = new InfluxDBValidator();
                LPS.UI.Common.Options.InfluxDBOptions influxDbOptionsToValidate = new()
                {
                    // Combine the provided InfluxDB options by the command and what in the config section to validate the final object
                    Enabled = updateInfluxDbOptions.Enabled ?? _influxDbOptions.Value.Enabled,
                    Url = !string.IsNullOrWhiteSpace(updateInfluxDbOptions.Url) ? updateInfluxDbOptions.Url : _influxDbOptions.Value.Url,
                    Token = !string.IsNullOrWhiteSpace(updateInfluxDbOptions.Token) ? updateInfluxDbOptions.Token : _influxDbOptions.Value.Token,
                    Organization = !string.IsNullOrWhiteSpace(updateInfluxDbOptions.Organization) ? updateInfluxDbOptions.Organization : _influxDbOptions.Value.Organization,
                    Bucket = !string.IsNullOrWhiteSpace(updateInfluxDbOptions.Bucket) ? updateInfluxDbOptions.Bucket : _influxDbOptions.Value.Bucket
                };

                var validationResults = influxDbValidator.Validate(influxDbOptionsToValidate);

                if (!validationResults.IsValid)
                {
                    _logger.Log(_runtimeOperationIdProvider.OperationId, "You must update the below properties to have a valid InfluxDB configuration. Updating the LPSAppSettings:InfluxDB section with the provided arguments will create an invalid InfluxDB configuration. You may run 'lps influxdb -h' to explore the options", LPSLoggingLevel.Warning);
                    validationResults.PrintValidationErrors();
                }
                else
                {
                    _influxDbOptions.Update(option =>
                    {
                        option.Enabled = influxDbOptionsToValidate.Enabled;
                        option.Url = influxDbOptionsToValidate.Url;
                        option.Token = influxDbOptionsToValidate.Token;
                        option.Organization = influxDbOptionsToValidate.Organization;
                        option.Bucket = influxDbOptionsToValidate.Bucket;
                    });
                }
            }, new InfluxDBBinder());
        }
    }
}