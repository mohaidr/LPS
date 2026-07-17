using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using LPS.Infrastructure.LPSClients.SessionManager;
using LPS.Infrastructure.VariableServices.GlobalVariableManager;

namespace LPS.Infrastructure.PlaceHolderService.Methods
{
    /// <summary>
    /// Shared read logic for $read and $readif. Resolves a value from a file, an environment variable,
    /// or a stored variable based on the <c>source</c> / <c>path</c> / <c>name</c> / <c>encoding</c>
    /// parameters, so both methods stay in lock-step without duplicating the source handling.
    /// </summary>
    internal static class SourceReader
    {
        public static async Task<string> ReadAsync(
            ParameterExtractorService @params,
            ISessionManager session,
            IVariableManager variables,
            string parameters,
            string sessionId,
            CancellationToken token)
        {
            // source can be: file|env|variable (default: file if 'path' provided, else variable)
            string source = await @params.ExtractStringAsync(parameters, "source", "", sessionId, token);
            string path = await @params.ExtractStringAsync(parameters, "path", "", sessionId, token);
            string name = await @params.ExtractStringAsync(parameters, "name", "", sessionId, token);
            string encoding = await @params.ExtractStringAsync(parameters, "encoding", "utf-8", sessionId, token);

            if (!string.IsNullOrEmpty(path) || string.Equals(source, "file", StringComparison.OrdinalIgnoreCase))
            {
                var enc = encoding.ToLowerInvariant() switch
                {
                    "utf8" or "utf-8" => Encoding.UTF8,
                    "unicode" or "utf-16" => Encoding.Unicode,
                    "ascii" => Encoding.ASCII,
                    _ => Encoding.UTF8
                };
                return File.Exists(path) ? await File.ReadAllTextAsync(path, enc, token) : string.Empty;
            }

            if (string.Equals(source, "env", StringComparison.OrdinalIgnoreCase))
            {
                return Environment.GetEnvironmentVariable(name) ?? string.Empty;
            }

            // variable
            var holder = await session.GetVariableAsync(sessionId, name, token) ?? await variables.GetAsync(name, token);
            return holder is null ? string.Empty : await holder.GetRawValueAsync(token);
        }
    }
}
