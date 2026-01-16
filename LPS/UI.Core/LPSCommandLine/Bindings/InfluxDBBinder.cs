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
    public class InfluxDBBinder : BinderBase<LPS.UI.Common.Options.InfluxDBOptions>
    {
        private Option<bool?> _enabledOption;
        private Option<string?> _urlOption;
        private Option<string?> _tokenOption;
        private Option<string?> _organizationOption;
        private Option<string?> _bucketOption;

        public InfluxDBBinder(
            Option<bool?>? enabledOption = null,
            Option<string?>? urlOption = null,
            Option<string?>? tokenOption = null,
            Option<string?>? organizationOption = null,
            Option<string?>? bucketOption = null)
        {
            _enabledOption = enabledOption ?? CommandLineOptions.LPSInfluxDBCommandOptions.EnabledOption;
            _urlOption = urlOption ?? CommandLineOptions.LPSInfluxDBCommandOptions.UrlOption;
            _tokenOption = tokenOption ?? CommandLineOptions.LPSInfluxDBCommandOptions.TokenOption;
            _organizationOption = organizationOption ?? CommandLineOptions.LPSInfluxDBCommandOptions.OrganizationOption;
            _bucketOption = bucketOption ?? CommandLineOptions.LPSInfluxDBCommandOptions.BucketOption;
        }

        protected override LPS.UI.Common.Options.InfluxDBOptions GetBoundValue(BindingContext bindingContext)
        {
            return new LPS.UI.Common.Options.InfluxDBOptions()
            {
                Enabled = bindingContext.ParseResult.GetValueForOption(_enabledOption),
                Url = bindingContext.ParseResult.GetValueForOption(_urlOption),
                Token = bindingContext.ParseResult.GetValueForOption(_tokenOption),
                Organization = bindingContext.ParseResult.GetValueForOption(_organizationOption),
                Bucket = bindingContext.ParseResult.GetValueForOption(_bucketOption)
            };
        }
    }
}