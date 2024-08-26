using LPS.Domain;
using LPS.UI.Core.UI.Build.Services;
using System;
using System.Collections.Generic;
using System.CommandLine.Binding;
using System.CommandLine;
using System.Linq;
using System.Text;
using System.CommandLine.Parsing;

namespace LPS.UI.Core.LPSCommandLine.Bindings
{
    public class TestPlanCommandBinder : BinderBase<TestPlan.SetupCommand>
    {
        private Option<string> _testNameOption;
        private Option<int> _numberOfClientsOption;
        private Option<int> _rampupPeriodOption;
        private Option<bool> _delayClientCreationOption;
        private Option<bool> _runInParallerOption;

        public TestPlanCommandBinder(
            Option<string>? testNameOption = null,
            Option<int>? numberOfClientsOption = null,
            Option<int>? rampupPeriodOption = null,
            Option<bool>? delayClientCreationOption = null,
            Option<bool>? runInParallerOption = null)
        {
            _testNameOption = testNameOption ?? CommandLineOptions.LPSCreateCommandOptions.TestNameOption;
            _numberOfClientsOption = numberOfClientsOption ?? CommandLineOptions.LPSCreateCommandOptions.NumberOfClientsOption;
            _rampupPeriodOption = rampupPeriodOption ?? CommandLineOptions.LPSCreateCommandOptions.RampupPeriodOption;
            _delayClientCreationOption = delayClientCreationOption ?? CommandLineOptions.LPSCreateCommandOptions.DelayClientCreation;
            _runInParallerOption = runInParallerOption ?? CommandLineOptions.LPSCreateCommandOptions.RunInParaller;
        }

        protected override TestPlan.SetupCommand GetBoundValue(BindingContext bindingContext) =>
            new TestPlan.SetupCommand
            {
                Name = bindingContext.ParseResult.GetValueForOption(_testNameOption),
                NumberOfClients = bindingContext.ParseResult.GetValueForOption(_numberOfClientsOption),
                RampUpPeriod = bindingContext.ParseResult.GetValueForOption(_rampupPeriodOption),
                DelayClientCreationUntilIsNeeded = bindingContext.ParseResult.GetValueForOption(_delayClientCreationOption),
                RunInParallel = bindingContext.ParseResult.GetValueForOption(_runInParallerOption),
            };
    }
}
