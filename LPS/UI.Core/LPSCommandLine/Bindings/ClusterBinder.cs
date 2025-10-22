using System.CommandLine;
using System.CommandLine.Binding;
using LPS.UI.Common.Options;

namespace LPS.UI.Core.LPSCommandLine.Bindings
{
    public class ClusterBinder : BinderBase<ClusterConfigurationOptions>
    {
        private static Option<string?>? _masterNodeIPOption;
        private static Option<int?>? _grpcPortOption;
        private static Option<int?>? _expectedWorkersOption;
        private static Option<bool?>? _masterNodeIsWorkerOption;

        public ClusterBinder(
            Option<string?>? masterNodeIPOption = null,
            Option<int?>? grpcPortOption = null,
            Option<int?>? expectedWorkersOption = null,
            Option<bool?>? masterNodeIsWorkerOption = null)
        {
            _masterNodeIPOption = masterNodeIPOption ?? CommandLineOptions.LPSClusterCommandOptions.MasterNodeIPOption;
            _grpcPortOption = grpcPortOption ?? CommandLineOptions.LPSClusterCommandOptions.GRPCPortOption;
            _expectedWorkersOption = expectedWorkersOption ?? CommandLineOptions.LPSClusterCommandOptions.ExpectedWorkersOption;
            _masterNodeIsWorkerOption = masterNodeIsWorkerOption ?? CommandLineOptions.LPSClusterCommandOptions.MasterNodeIsWorkerOption;
        }

        protected override ClusterConfigurationOptions GetBoundValue(BindingContext ctx) =>
            new ClusterConfigurationOptions
            {
                MasterNodeIP = ctx.ParseResult.GetValueForOption(_masterNodeIPOption),
                GRPCPort = ctx.ParseResult.GetValueForOption(_grpcPortOption),
                ExpectedNumberOfWorkers = ctx.ParseResult.GetValueForOption(_expectedWorkersOption),
                MasterNodeIsWorker = ctx.ParseResult.GetValueForOption(_masterNodeIsWorkerOption),
            };
    }
}
