#nullable enable
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using LPS.Domain.Common;
using LPS.Infrastructure.LPSClients.SessionManager;

namespace LPS.Infrastructure.LPSClients.GlobalVariableManager
{
    public partial class VariableManager : IVariableManager
    {
        private readonly ConcurrentDictionary<string, IVariableHolder> _variables = new();

        public void AddVariable(string variableName, IVariableHolder variableHolder)
        {
            if (string.IsNullOrWhiteSpace(variableName))
                throw new ArgumentException("Variable name cannot be null or whitespace.", nameof(variableName));

            if (variableHolder == null)
                throw new ArgumentNullException(nameof(variableHolder), "Variable holder cannot be null.");

            if (!_variables.TryAdd(variableName, variableHolder))
            {
                throw new InvalidOperationException($"Variable '{variableName}' already exists.");
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
