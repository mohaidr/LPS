#nullable enable
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using LPS.Domain.Common;
using LPS.Domain.Common.Interfaces;
using LPS.Infrastructure.LPSClients.SessionManager;

namespace LPS.Infrastructure.LPSClients.GlobalVariableManager
{
    public partial class VariableManager(IRuntimeOperationIdProvider operationProvider, ILogger logger) : IVariableManager
    {
        private readonly ConcurrentDictionary<string, IVariableHolder> _variables = new();
        private readonly IRuntimeOperationIdProvider _operationIdProvider= operationProvider;
        private readonly ILogger _logger= logger;
        public async Task AddVariableAsync(string variableName, IVariableHolder variableHolder, CancellationToken token = default)
        {
            if (string.IsNullOrWhiteSpace(variableName))
                throw new ArgumentException("Variable name cannot be null or whitespace.", nameof(variableName));

            if (variableHolder == null)
                throw new ArgumentNullException(nameof(variableHolder), "Variable holder cannot be null.");

            if (!_variables.TryAdd(variableName, variableHolder))
            {
                await _logger.LogAsync(_operationIdProvider.OperationId, $" Variable '{{variableName}}' already exists and will be overridden", LPSLoggingLevel.Warning, token);
                // Override the existing variable
                _variables[variableName] = variableHolder;
            }
        }

        public IVariableHolder GetVariable(string variableName)
        {
            if (_variables.TryGetValue(variableName, out var variableHolder))
            {
                return variableHolder;
            }

            throw new KeyNotFoundException($"Variable '{variableName}' not found.");
        }
    }
}
