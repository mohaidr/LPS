using LPS.Domain;
using LPS.UI.Core.UI.Build.Services;
using System;
using System.Collections.Generic;
using System.CommandLine.Binding;
using System.CommandLine;
using System.Linq;
using System.Text;
using System.CommandLine.Parsing;

namespace LPS.UI.Core.LPSCommandLine
{
    public class LPSTestPlanCommandBinder : BinderBase<LPSTestPlan.SetupCommand>
    {
        public static Option<string> _testNameOption;
        public static Option<int> _numberOfClientsOption;
        public static Option<int> _rampupPeriodOption;
        public static Option<int> _maxConnectionsPerServerOption;
        public static Option<int> _poolConnectionLifeTimeOption;
        public static Option<int> _poolConnectionIdelTimeoutOption;
        public static Option<int> _clientTimeoutOption;
        public static Option<bool> _delayClientCreationOption;
        public static Option<bool> _runInParallerOption;


        public LPSTestPlanCommandBinder(Option<string> testNameOption,
        Option<int> numberOfClientsOption,
        Option<int> rampupPeriodOption,
        Option<int> maxConnectionsPerServerOption,
        Option<int> poolConnectionLifeTimeOption,
        Option<int> poolConnectionIdelTimeoutOption,
        Option<int> clientTimeoutOption,
        Option<bool> delayClientCreationOption,
        Option<bool> runInParallerOption)
        {
            _testNameOption = testNameOption;
            _numberOfClientsOption = numberOfClientsOption;
            _rampupPeriodOption = rampupPeriodOption;
            _maxConnectionsPerServerOption= maxConnectionsPerServerOption;
            _poolConnectionIdelTimeoutOption = poolConnectionIdelTimeoutOption;
            _poolConnectionLifeTimeOption = poolConnectionLifeTimeOption;
            _clientTimeoutOption= clientTimeoutOption;
            _delayClientCreationOption= delayClientCreationOption;
            _runInParallerOption=runInParallerOption;
        }

        protected override LPSTestPlan.SetupCommand GetBoundValue(BindingContext bindingContext) =>
            new LPSTestPlan.SetupCommand
            {
                Name = bindingContext.ParseResult.GetValueForOption(_testNameOption),
                NumberOfClients = bindingContext.ParseResult.GetValueForOption(_numberOfClientsOption),
                RampUpPeriod = bindingContext.ParseResult.GetValueForOption(_rampupPeriodOption),
                DelayClientCreationUntilIsNeeded = bindingContext.ParseResult.GetValueForOption(_delayClientCreationOption),
                ClientTimeout = bindingContext.ParseResult.GetValueForOption(_clientTimeoutOption),
                MaxConnectionsPerServer = bindingContext.ParseResult.GetValueForOption(_maxConnectionsPerServerOption),
                PooledConnectionIdleTimeout = bindingContext.ParseResult.GetValueForOption(_poolConnectionIdelTimeoutOption),
                PooledConnectionLifetime = bindingContext.ParseResult.GetValueForOption(_poolConnectionLifeTimeOption),
                RunInParallel = bindingContext.ParseResult.GetValueForOption(_runInParallerOption),
            };
    }
}
