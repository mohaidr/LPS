
using System;
using System.Threading;
using System.Threading.Tasks;
using LPS.Domain.Common;
using LPS.Domain.Common.Interfaces;
using LPS.Domain.Domain.Common.Enums;
using LPS.Infrastructure.VariableServices.GlobalVariableManager;
using LPS.Infrastructure.VariableServices.VariableHolders;

namespace LPS.Infrastructure.PlaceHolderService.Methods
{
    public abstract class MethodBase : IPlaceholderMethod
    {
        protected readonly ParameterExtractorService _params;
        protected readonly ILogger _logger;
        protected readonly IRuntimeOperationIdProvider _op;
        protected readonly IVariableManager _variables;
        protected readonly Lazy<IPlaceholderResolverService> _resolver; // Use Lazy

        protected MethodBase(
            ParameterExtractorService @params,
            ILogger logger,
            IRuntimeOperationIdProvider op,
            IVariableManager variables,
            Lazy<IPlaceholderResolverService> resolver) // Inject Lazy
        {
            _params = @params;
            _logger = logger;
            _op = op;
            _variables = variables;
            _resolver = resolver;
        }

        public abstract string Name { get; }
        public abstract Task<string> ExecuteAsync(string parameters, string sessionId, CancellationToken token);

        protected async Task StoreVariableIfNeededAsync(string variableName, string value, CancellationToken token)
        {
            if (string.IsNullOrWhiteSpace(variableName)) return;

            var holder = await new StringVariableHolder.VBuilder(_resolver.Value, _logger, _op) // Use .Value
                .WithType(VariableType.String)
                .WithRawValue(value ?? string.Empty)
                .SetGlobal()
                .BuildAsync(token);

            await _variables.PutAsync(variableName, holder, token);
        }

        protected static string Truncate(string? value, int max = 128) =>
            string.IsNullOrEmpty(value) ? "<empty>" :
            value.Length <= max ? value : value.Substring(0, max) + $"...(+{value.Length - max})";

        protected static string PadBase64(string s) =>
            string.IsNullOrEmpty(s) ? s : s.PadRight(s.Length + (4 - s.Length % 4) % 4, '=');
    }
}
