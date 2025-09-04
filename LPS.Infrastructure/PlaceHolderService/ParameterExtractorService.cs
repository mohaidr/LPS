using LPS.Domain.Common.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace LPS.Infrastructure.PlaceHolderService
{
    public class ParameterExtractorService
    {
        private readonly Lazy<IPlaceholderResolverService> _resolver;
        private readonly IRuntimeOperationIdProvider _runtimeOperationIdProvider;
        private readonly ILogger _logger;

        public ParameterExtractorService(
            Lazy<IPlaceholderResolverService> resolver,
            IRuntimeOperationIdProvider runtimeOperationIdProvider,
            ILogger logger)
        {
            _resolver = resolver;
            _runtimeOperationIdProvider = runtimeOperationIdProvider ?? throw new ArgumentNullException(nameof(runtimeOperationIdProvider));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        }


        public async Task<int> ExtractNumberAsync(string parameters, string key, int defaultValue, string sessionId, CancellationToken token)
        {
            if (string.IsNullOrEmpty(parameters))
            {
                return defaultValue;
            }

            var keyValuePairs = parameters.Split(',');
            foreach (var pair in keyValuePairs)
            {
                var parts = pair.Split('=', 2);
                if (parts.Length == 2 && parts[0].Trim() == key)
                {
                    int resolvedValue = await _resolver.Value.ResolvePlaceholdersAsync<int>(parts[1].Trim(), sessionId, token);
                    return resolvedValue;
                }
            }

            return defaultValue;
        }

        public async Task<string> ExtractStringAsync(string parameters, string key, string defaultValue, string sessionId, CancellationToken token)
        {
            if (string.IsNullOrEmpty(parameters))
                return defaultValue;

            var keyValuePairs = parameters.Split(',');
            foreach (var pair in keyValuePairs)
            {
                var parts = pair.Split('=', 2);
                if (parts.Length == 2 && parts[0].Trim() == key)
                {
                    return await _resolver.Value.ResolvePlaceholdersAsync<string>(parts[1].Trim(), sessionId, token);
                }
            }

            return defaultValue;
        }
    }

}
