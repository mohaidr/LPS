using LPS.Domain;
using LPS.UI.Core.Build.Services;
using System;
using System.Collections.Generic;
using System.CommandLine.Binding;
using System.CommandLine;
using System.Linq;
using System.Text;
using System.CommandLine.Parsing;
using LPS.UI.Common.Options;
using LPS.Domain.Common.Interfaces;
using LPS.Infrastructure.Watchdog;
using LPS.Infrastructure.Common.Interfaces;

namespace LPS.UI.Core.LPSCommandLine.Bindings
{
    public class HttpClientBinder : BinderBase<HttpClientOptions>
    {
        private static Option<int?>? _maxConnectionsPerServerption;
        private static Option<int?>? _poolConnectionLifeTimeOption;
        private static Option<int?>? _poolConnectionIdleTimeoutOption;
        private static Option<int?>? _clientTimeoutOption;
        private static Option<string?>? _serverTimeHeaderOption;
        private static Option<ServerTimeFormat?>? _serverTimeFormatOption;




        public HttpClientBinder(Option<int?>? maxConnectionsPerServerption= null,
         Option<int?>? poolConnectionLifeTimeOption = null,
         Option<int?>? poolConnectionIdleTimeoutOption = null,
         Option<int?>? clientTimeoutOption = null,
         Option<string?>? serverTimeHeaderOption = null,
         Option<ServerTimeFormat?>? serverTimeFormatOption = null)
        {
            _maxConnectionsPerServerption = maxConnectionsPerServerption ?? CommandLineOptions.LPSHttpClientCommandOptions.MaxConnectionsPerServerOption;
            _poolConnectionLifeTimeOption = poolConnectionLifeTimeOption ?? CommandLineOptions.LPSHttpClientCommandOptions.PoolConnectionLifetimeOption;
            _poolConnectionIdleTimeoutOption = poolConnectionIdleTimeoutOption ?? CommandLineOptions.LPSHttpClientCommandOptions.PoolConnectionIdleTimeoutOption;
            _clientTimeoutOption = clientTimeoutOption ?? CommandLineOptions.LPSHttpClientCommandOptions.ClientTimeoutOption;
            _serverTimeHeaderOption = serverTimeHeaderOption ?? CommandLineOptions.LPSHttpClientCommandOptions.ServerTimeHeaderOption;
            _serverTimeFormatOption = serverTimeFormatOption ?? CommandLineOptions.LPSHttpClientCommandOptions.ServerTimeFormatOption;
        }

        protected override HttpClientOptions GetBoundValue(BindingContext bindingContext) =>
            new HttpClientOptions
            {
                MaxConnectionsPerServer = bindingContext.ParseResult.GetValueForOption(_maxConnectionsPerServerption),
                PooledConnectionLifeTimeInSeconds = bindingContext.ParseResult.GetValueForOption(_poolConnectionLifeTimeOption),
                PooledConnectionIdleTimeoutInSeconds = bindingContext.ParseResult.GetValueForOption(_poolConnectionIdleTimeoutOption),
                ClientTimeoutInSeconds = bindingContext.ParseResult.GetValueForOption(_clientTimeoutOption),
                ServerTimeHeader = bindingContext.ParseResult.GetValueForOption(_serverTimeHeaderOption),
                ServerTimeFormat = bindingContext.ParseResult.GetValueForOption(_serverTimeFormatOption),
            };
    }
}
