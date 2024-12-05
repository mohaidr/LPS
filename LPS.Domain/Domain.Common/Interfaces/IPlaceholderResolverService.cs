using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using YamlDotNet.Core.Tokens;

namespace LPS.Domain.Common.Interfaces
{
    public interface IPlaceholderResolverService
    {
        Task<string> ResolvePlaceholdersAsync(string input, string sessionId, CancellationToken Token);
        static bool IsSupportedPlaceHolderMethod(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || !value.StartsWith("$") || !value.EndsWith(")"))
                return false;

            // Extract function name
            int openParenIndex = value.IndexOf('(');
            if (openParenIndex == -1)
                return false;

            string functionName = value.Substring(1, openParenIndex - 1).Trim().ToLowerInvariant(); // Skip $

            // Check against supported methods
            return functionName switch
            {
                "random" => true,
                "randomnumber" => true,
                "timestamp" => true,
                "guid" => true,
                "base64encode" => true,
                "hash" => true,
                "customvariable" => true,
                "read" => true,
                _ => false // Unsupported method
            };
        }
    }
}

