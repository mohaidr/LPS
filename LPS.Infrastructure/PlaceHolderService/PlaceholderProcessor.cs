
using AsyncKeyedLock;
using LPS.Domain.Common;
using LPS.Domain.Common.Interfaces;
using LPS.Infrastructure.Caching;
using LPS.Infrastructure.Common;
using LPS.Infrastructure.LPSClients.CachService;
using LPS.Infrastructure.LPSClients.SessionManager;
using LPS.Infrastructure.PlaceHolderService.Methods;
using LPS.Infrastructure.VariableServices.GlobalVariableManager;
using LPS.Infrastructure.VariableServices.VariableHolders;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace LPS.Infrastructure.PlaceHolderService
{
    public sealed class PlaceholderProcessor: IPlaceholderProcessor
    {
        private readonly ISessionManager _sessionManager;
        private readonly IVariableManager _variableManager;
        private readonly IRuntimeOperationIdProvider _runtimeOperationIdProvider;
        private readonly ILogger _logger;
        private readonly Dictionary<string, IPlaceholderMethod> _methods;


        public PlaceholderProcessor(
            IEnumerable<IPlaceholderMethod> methods,
            ISessionManager sessionManager,
            IVariableManager variableManager,
            IRuntimeOperationIdProvider runtimeOperationIdProvider,
            ILogger logger)
        {
            _sessionManager = sessionManager;
            _variableManager = variableManager;
            _runtimeOperationIdProvider = runtimeOperationIdProvider;
            _logger = logger;
            _methods = (methods ?? Array.Empty<IPlaceholderMethod>()).ToDictionary(m => m.Name, StringComparer.OrdinalIgnoreCase);
        }

        public async Task<string> ProcessPlaceholderAsync(string placeholder, string sessionId, CancellationToken token)
        {
            bool isMethod = placeholder.EndsWith(")");
            if (isMethod)
                return await ProcessMethodAsync(placeholder, sessionId, token);
            else
                return await ProcessVariableAsync(placeholder, sessionId, token);
        }

        private async Task<string> ProcessMethodAsync(string placeholder, string sessionId, CancellationToken token)
        {
            int i = placeholder.IndexOf('(');
            string name = placeholder.Substring(0, i).Trim();
            string args = placeholder.Substring(i + 1, placeholder.Length - i - 2).Trim();

            if (_methods.TryGetValue(name, out var method))
            {
                return await method.ExecuteAsync(args, sessionId, token);
            }
            // alias support (e.g., datetime->timestamp, loopcounter->iterate)
            if (string.Equals(name, "datetime", StringComparison.OrdinalIgnoreCase) && _methods.TryGetValue("timestamp", out var ts))
                return await ts.ExecuteAsync(args, sessionId, token);
            if (string.Equals(name, "loopcounter", StringComparison.OrdinalIgnoreCase) && _methods.TryGetValue("iterate", out var it))
                return await it.ExecuteAsync(args, sessionId, token);

            await _logger.LogAsync(_runtimeOperationIdProvider.OperationId, $"Unknown function '{name}'.", LPSLoggingLevel.Warning, token);
            return string.Empty;
        }

        private async Task<string> ProcessVariableAsync(string placeholder, string sessionId, CancellationToken token)
        {
            string variableName = placeholder;
            string? path = null;

            if (placeholder.Contains('.') || placeholder.Contains('/') || placeholder.Contains('['))
            {
                int splitIndex = placeholder.IndexOfAny(new[] {'.', '/', '['});
                variableName = placeholder.Substring(0, splitIndex);
                path = placeholder.Substring(splitIndex);
            }

            var variableHolder = await _sessionManager.GetVariableAsync(sessionId, variableName, token)
                                 ?? await _variableManager.GetAsync(variableName, token);

            if (variableHolder == null)
            {
                await _logger.LogAsync(_runtimeOperationIdProvider.OperationId, $"Variable '{variableName}' not found.", LPSLoggingLevel.Warning, token);
                return $"${{{variableName}{path}}}";
            }

            if (string.IsNullOrWhiteSpace(path))
                return await variableHolder.GetRawValueAsync(token);

            if (variableHolder is IObjectVariableHolder objHolder)
                return await objHolder.GetValueAsync(path, sessionId, token);

            return await variableHolder.GetRawValueAsync(token);
        }
    }
}
