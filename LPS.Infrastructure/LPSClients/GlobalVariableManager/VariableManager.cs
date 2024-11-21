#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LPS.Infrastructure.LPSClients.GlobalVariableManager
{
    using System;
    using System.Collections.Concurrent;
    using System.Text.RegularExpressions;

    public class VariableManager : IVariableManager
    {
        private readonly ConcurrentDictionary<string, Func<object?>> _variables = new();

        // Regex patterns for dynamic methods
        private const string RandomStringPattern = @"^RandomString\(\)$";
        private const string RandomNumberPattern = @"^RandomNumber\((\d+),(\d+)\)$";

        public void AddVariable(string variableName, string methodOrValue)
        {
            if (string.IsNullOrWhiteSpace(variableName))
                throw new ArgumentException("Variable name cannot be null or whitespace.", nameof(variableName));

            Func<object?> valueGenerator = ParseMethodOrValue(methodOrValue);

            if (!_variables.TryAdd(variableName, valueGenerator))
            {
                throw new InvalidOperationException($"Variable '{variableName}' already exists.");
            }
        }

        public object? GetVariable(string variableName)
        {
            if (_variables.TryGetValue(variableName, out var valueGenerator))
            {
                return valueGenerator.Invoke();
            }

            throw new KeyNotFoundException($"Variable '{variableName}' not found.");
        }

        private static Func<object?> ParseMethodOrValue(string methodOrValue)
        {
            // Check for dynamic methods
            if (Regex.IsMatch(methodOrValue, RandomStringPattern))
            {
                return () => Guid.NewGuid().ToString(); // Generate random string
            }

            var match = Regex.Match(methodOrValue, RandomNumberPattern);
            if (match.Success)
            {
                int min = int.Parse(match.Groups[1].Value);
                int max = int.Parse(match.Groups[2].Value);
                return () => new Random().Next(min, max); // Generate random number in range
            }

            // If no method detected, treat as a plain text value
            return () => methodOrValue;
        }
    }

}
