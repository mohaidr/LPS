using LPS.Domain.Common.Interfaces;
using LPS.Infrastructure.Logger;
using LPS.UI.Common;
using LPS.UI.Common.Extensions;
using LPS.UI.Common.Options;
using LPS.UI.Core.LPSCommandLine.Bindings;
using LPS.UI.Core.LPSValidators;
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
    internal class LPSHttpClientCLICommand : ILPSCLICommand
    {
        private Command _rootLpsCliCommand;
        private Command _httpClientCommand;
        private string[] _args;
        IWritableOptions<LPSHttpClientOptions> _clientOptions;
        ILPSLogger _logger;
        ILPSRuntimeOperationIdProvider _runtimeOperationIdProvider;
        public LPSHttpClientCLICommand(Command rootLpsCliCommand, ILPSLogger logger, ILPSRuntimeOperationIdProvider runtimeOperationIdProvider, IWritableOptions<LPSHttpClientOptions> clientOptions, string[] args) 
        {
            _rootLpsCliCommand = rootLpsCliCommand;
            _args = args;
            _clientOptions = clientOptions;
            _logger = logger;
           _runtimeOperationIdProvider= runtimeOperationIdProvider;
            Setup();
        }
        private void Setup()
        {
            _httpClientCommand = new Command("httpclient", "Configure the http client");
            LPSCommandLineOptions.AddOptionsToCommand(_httpClientCommand, typeof(LPSCommandLineOptions.LPSHttpClientCommandOptions));
            _rootLpsCliCommand.AddCommand(_httpClientCommand);
        }

        public void Execute(CancellationToken cancellationToken)
        {

            _httpClientCommand.SetHandler((updatedClientOptions) =>
            {
                var httpClientValidator = new LPSHttpClientValidator();
                LPSHttpClientOptions clientOptions = new LPSHttpClientOptions();
                clientOptions.MaxConnectionsPerServer = updatedClientOptions.MaxConnectionsPerServer ?? _clientOptions.Value.MaxConnectionsPerServer;
                clientOptions.PooledConnectionLifeTimeInSeconds = updatedClientOptions.PooledConnectionLifeTimeInSeconds ?? _clientOptions.Value.PooledConnectionLifeTimeInSeconds;
                clientOptions.PooledConnectionIdleTimeoutInSeconds = updatedClientOptions.PooledConnectionIdleTimeoutInSeconds ?? _clientOptions.Value.PooledConnectionIdleTimeoutInSeconds;
                clientOptions.ClientTimeoutInSeconds = updatedClientOptions.ClientTimeoutInSeconds ?? _clientOptions.Value.ClientTimeoutInSeconds;
                var validationResults = httpClientValidator.Validate(clientOptions);

                if (!validationResults.IsValid)
                {
                    _logger.Log(_runtimeOperationIdProvider.OperationId, "You must update the below properties to have a valid http client configuration. Updating the LPSAppSettings:LPSHttpClientConfiguration section with the provided arguements will create an invalid http client configuration. You may run 'lps httpclient -h' to explore the options", LPSLoggingLevel.Warning);
                    validationResults.PrintValidationErrors();
                }
                else
                {
                    _clientOptions.Update(option =>
                    {
                        option.MaxConnectionsPerServer = clientOptions.MaxConnectionsPerServer;
                        option.PooledConnectionLifeTimeInSeconds = clientOptions.PooledConnectionLifeTimeInSeconds;
                        option.PooledConnectionIdleTimeoutInSeconds = clientOptions.PooledConnectionIdleTimeoutInSeconds;
                        option.ClientTimeoutInSeconds = clientOptions.ClientTimeoutInSeconds;
                    });
                }
            }, new LPSHttpClientBinder());

            _rootLpsCliCommand.Invoke(_args);
        }
    }
}
