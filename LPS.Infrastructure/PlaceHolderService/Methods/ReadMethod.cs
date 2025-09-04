
using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using LPS.Domain.Common;
using LPS.Domain.Common.Interfaces;
using LPS.Infrastructure.LPSClients.SessionManager;
using LPS.Infrastructure.VariableServices.GlobalVariableManager;

namespace LPS.Infrastructure.PlaceHolderService.Methods
{
    public sealed class ReadMethod : MethodBase
    {
        private readonly ISessionManager _session;
        public ReadMethod(ISessionManager sessionManager,
                          ParameterExtractorService p, ILogger l, IRuntimeOperationIdProvider op, IVariableManager v, Lazy<IPlaceholderResolverService> r)
            : base(p,l,op,v,r)
        {
            _session = sessionManager;
        }

        public override string Name => "read";

        public override async Task<string> ExecuteAsync(string parameters, string sessionId, CancellationToken token)
        {
            string variableName = string.Empty;
            try
            {
                // source can be: file|env|variable (default: file if 'path' provided, else variable)
                string source = await _params.ExtractStringAsync(parameters, "source", "", sessionId, token);
                string path = await _params.ExtractStringAsync(parameters, "path", "", sessionId, token);
                string name = await _params.ExtractStringAsync(parameters, "name", "", sessionId, token);
                string encoding = await _params.ExtractStringAsync(parameters, "encoding", "utf-8", sessionId, token);
                variableName = await _params.ExtractStringAsync(parameters, "variable", "", sessionId, token);

                string result = string.Empty;
                if (!string.IsNullOrEmpty(path) || string.Equals(source, "file", StringComparison.OrdinalIgnoreCase))
                {
                    var enc = encoding.ToLowerInvariant() switch
                    {
                        "utf8" or "utf-8" => Encoding.UTF8,
                        "unicode" or "utf-16" => Encoding.Unicode,
                        "ascii" => Encoding.ASCII,
                        _ => Encoding.UTF8
                    };
                    result = File.Exists(path) ? await File.ReadAllTextAsync(path, enc, token) : string.Empty;
                }
                else if (string.Equals(source, "env", StringComparison.OrdinalIgnoreCase))
                {
                    result = Environment.GetEnvironmentVariable(name) ?? string.Empty;
                }
                else // variable
                {
                    var holder = await _session.GetVariableAsync(sessionId, name, token) ?? await _variables.GetAsync(name, token);
                    result = holder is null ? string.Empty : await holder.GetRawValueAsync(token);
                }

                await StoreVariableIfNeededAsync(variableName, result, token);
                return result;
            }
            catch (Exception ex)
            {
                await _logger.LogAsync(_op.OperationId, $"read failed. {ex}", LPSLoggingLevel.Error, token);
                await StoreVariableIfNeededAsync(variableName, string.Empty, token);
                return string.Empty;
            }
        }
    }
}
