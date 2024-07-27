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
using LPS.Infrastructure.Watchdog;

namespace LPS.UI.Core.LPSCommandLine.Bindings
{
    public class LPSHttpClientBinder : BinderBase<LPSHttpClientOptions>
    {
        private static Option<int?>? _maxConnectionsPerServerption;
        private static Option<int?>? _poolConnectionLifeTimeOption;
        private static Option<int?>? _poolConnectionIdelTimeoutOption;
        private static Option<int?>? _clientTimeoutOption;




        public LPSHttpClientBinder(Option<int?>? maxConnectionsPerServerption= null,
         Option<int?>? poolConnectionLifeTimeOption = null,
         Option<int?>? poolConnectionIdelTimeoutOption = null,
         Option<int?>? clientTimeoutOption = null)
        {
            _maxConnectionsPerServerption = maxConnectionsPerServerption ?? LPSCommandLineOptions.LPSHttpClientCommandOptions.MaxConnectionsPerServerption;
            _poolConnectionLifeTimeOption = poolConnectionLifeTimeOption ?? LPSCommandLineOptions.LPSHttpClientCommandOptions.PoolConnectionLifeTimeOption;
            _poolConnectionIdelTimeoutOption = poolConnectionIdelTimeoutOption ?? LPSCommandLineOptions.LPSHttpClientCommandOptions.PoolConnectionIdelTimeoutOption;
            _clientTimeoutOption = clientTimeoutOption ?? LPSCommandLineOptions.LPSHttpClientCommandOptions.ClientTimeoutOption;
        }

        protected override LPSHttpClientOptions GetBoundValue(BindingContext bindingContext) =>
            new LPSHttpClientOptions
            {
                MaxConnectionsPerServer = bindingContext.ParseResult.GetValueForOption(_maxConnectionsPerServerption),
                PooledConnectionLifeTimeInSeconds = bindingContext.ParseResult.GetValueForOption(_poolConnectionLifeTimeOption),
                PooledConnectionIdleTimeoutInSeconds = bindingContext.ParseResult.GetValueForOption(_poolConnectionIdelTimeoutOption),
                ClientTimeoutInSeconds = bindingContext.ParseResult.GetValueForOption(_clientTimeoutOption),
            };
    }
}
