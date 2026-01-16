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

namespace LPS.UI.Core.LPSCommandLine.Bindings
{
    public class DashboardBinder : BinderBase<DashboardConfigurationOptions>
    {
        private Option<bool?> _builtInDashboardOption;
        private Option<int?> _portOption;
        private Option<int?> _refreshRateOption;

        public DashboardBinder(
            Option<bool?>? builtInDashboardOption = null,
            Option<int?>? portOption = null,
            Option<int?>? refreshRateOption = null)
        {
            _builtInDashboardOption = builtInDashboardOption ?? CommandLineOptions.LPSDashboardCommandOptions.BuiltInDashboardOption;
            _portOption = portOption ?? CommandLineOptions.LPSDashboardCommandOptions.PortOption;
            _refreshRateOption = refreshRateOption ?? CommandLineOptions.LPSDashboardCommandOptions.RefreshRateOption;
        }

        protected override DashboardConfigurationOptions GetBoundValue(BindingContext bindingContext)
        {
            return new DashboardConfigurationOptions()
            {
                BuiltInDashboard = bindingContext.ParseResult.GetValueForOption(_builtInDashboardOption),
                Port = bindingContext.ParseResult.GetValueForOption(_portOption),
                RefreshRate = bindingContext.ParseResult.GetValueForOption(_refreshRateOption)
            };
        }
    }
}