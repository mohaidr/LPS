using LPS.Domain.Common.Interfaces;
using LPS.Infrastructure.Logger;
using LPS.UI.Common;
using LPS.UI.Common.Extensions;
using LPS.UI.Common.Options;
using LPS.UI.Core.LPSCommandLine.Bindings;
using LPS.UI.Core.LPSValidators;
using LPS.UI.Core.Build.Services;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.CommandLine;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using LPS.Infrastructure.LPSClients.HeaderServices; // HeaderValidationMode

namespace LPS.UI.Core.LPSCommandLine.Commands
{
    internal class HttpClientCliCommand : ICliCommand
    {
        private Command _rootLpsCliCommand;
        private Command _httpClientCommand;
        public Command Command => _httpClientCommand;

        private readonly IWritableOptions<HttpClientOptions> _clientOptions;
        private readonly ILogger _logger;
        private readonly IRuntimeOperationIdProvider _runtimeOperationIdProvider;

#pragma warning disable CS8618
        public HttpClientCliCommand(
            Command rootLpsCliCommand,
            ILogger logger,
            IRuntimeOperationIdProvider runtimeOperationIdProvider,
            IWritableOptions<HttpClientOptions> clientOptions)
        {
            _rootLpsCliCommand = rootLpsCliCommand;
            _clientOptions = clientOptions;
            _logger = logger;
            _runtimeOperationIdProvider = runtimeOperationIdProvider;
            Setup();
        }
#pragma warning restore CS8618

        private void Setup()
        {
            _httpClientCommand = new Command("httpclient", "Configure the http client");
            CommandLineOptions.AddOptionsToCommand(_httpClientCommand, typeof(CommandLineOptions.LPSHttpClientCommandOptions));
            _rootLpsCliCommand.AddCommand(_httpClientCommand);
        }

        public void SetHandler(CancellationToken cancellationToken)
        {
            _httpClientCommand.SetHandler((HttpClientOptions updatedClientOptions) =>
            {
                var httpClientValidator = new HttpClientValidator();

                // Build a candidate config using CLI overrides when provided, otherwise keep current values
                var clientOptions = new HttpClientOptions
                {
                    MaxConnectionsPerServer = updatedClientOptions.MaxConnectionsPerServer ?? _clientOptions.Value.MaxConnectionsPerServer,
                    PooledConnectionLifeTimeInSeconds = updatedClientOptions.PooledConnectionLifeTimeInSeconds ?? _clientOptions.Value.PooledConnectionLifeTimeInSeconds,
                    PooledConnectionIdleTimeoutInSeconds = updatedClientOptions.PooledConnectionIdleTimeoutInSeconds ?? _clientOptions.Value.PooledConnectionIdleTimeoutInSeconds,
                    ClientTimeoutInSeconds = updatedClientOptions.ClientTimeoutInSeconds ?? _clientOptions.Value.ClientTimeoutInSeconds,
                    HeaderValidationMode = updatedClientOptions.HeaderValidationMode ?? _clientOptions.Value.HeaderValidationMode,
                    AllowHostOverride = updatedClientOptions.AllowHostOverride ?? _clientOptions.Value.AllowHostOverride
                };

                var validationResults = httpClientValidator.Validate(clientOptions);

                if (!validationResults.IsValid)
                {
                    _logger.Log(
                        _runtimeOperationIdProvider.OperationId,
                        "Invalid HTTP client configuration. Updating LPSAppSettings:HttpClient with the provided arguments would result in an invalid configuration. Run 'lps httpclient -h' to see available options.",
                        LPSLoggingLevel.Warning);

                    validationResults.PrintValidationErrors();
                    return;
                }

                _clientOptions.Update(option =>
                {
                    option.MaxConnectionsPerServer = clientOptions.MaxConnectionsPerServer;
                    option.PooledConnectionLifeTimeInSeconds = clientOptions.PooledConnectionLifeTimeInSeconds;
                    option.PooledConnectionIdleTimeoutInSeconds = clientOptions.PooledConnectionIdleTimeoutInSeconds;
                    option.ClientTimeoutInSeconds = clientOptions.ClientTimeoutInSeconds;
                    option.HeaderValidationMode = clientOptions.HeaderValidationMode;
                    option.AllowHostOverride = clientOptions.AllowHostOverride;
                });

                _logger.Log(
                    _runtimeOperationIdProvider.OperationId,
                    $"HTTP client configuration updated. MaxConnectionsPerServer={clientOptions.MaxConnectionsPerServer}, " +
                    $"PooledConnectionLifeTimeInSeconds={clientOptions.PooledConnectionLifeTimeInSeconds}, " +
                    $"PooledConnectionIdleTimeoutInSeconds={clientOptions.PooledConnectionIdleTimeoutInSeconds}, " +
                    $"ClientTimeoutInSeconds={clientOptions.ClientTimeoutInSeconds}, " +
                    $"HeaderValidationMode={clientOptions.HeaderValidationMode}, " +
                    $"AllowHostOverride={clientOptions.AllowHostOverride}",
                    LPSLoggingLevel.Information);

            }, new HttpClientBinder());
        }
    }
}
