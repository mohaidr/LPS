
using LPS.Domain.Common.Interfaces;
using LPS.Domain.Common;
using LPS.Infrastructure.Caching;
using LPS.Infrastructure.Common;
using LPS.Infrastructure.LPSClients.CachService;
using LPS.Infrastructure.LPSClients.SessionManager;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using LPS.Infrastructure.VariableServices.GlobalVariableManager;
using LPS.Infrastructure.VariableServices.VariableHolders;
using LPS.Domain.Domain.Common.Enums;
using AsyncKeyedLock;
using System.Security.Claims;

namespace LPS.Infrastructure.VariableServices.PlaceHolderService
{
    internal partial class PlaceholderProcessor
    {
        private readonly ISessionManager _sessionManager;
        private readonly IVariableManager _variableManager;
        private readonly ICacheService<string> _memoryCacheService;
        private readonly IRuntimeOperationIdProvider _runtimeOperationIdProvider;
        private readonly ILogger _logger;
        private readonly ParameterExtractorService _paramService;
        private readonly IPlaceholderResolverService _placeholderResolverService;
        private static readonly AsyncKeyedLocker<string> _locker = new();

        public PlaceholderProcessor(
            ParameterExtractorService paramService,
            ISessionManager sessionManager,
            ICacheService<string> memoryCacheService,
            IVariableManager variableManager,
            IPlaceholderResolverService placeholderResolverService,
            IRuntimeOperationIdProvider runtimeOperationIdProvider,
            ILogger logger)
        {
            _sessionManager = sessionManager;
            _memoryCacheService = memoryCacheService;
            _variableManager = variableManager;
            _placeholderResolverService = placeholderResolverService;
            _runtimeOperationIdProvider = runtimeOperationIdProvider;
            _logger = logger;
            _paramService = paramService;
        }

        public async Task<string> ProcessPlaceholderAsync(string placeholder, string sessionId, CancellationToken token)
        {
            int openParenIndex = placeholder.IndexOf('(');
            if (openParenIndex != -1)
            {
                return await ProcessMethodAsync(placeholder, sessionId, token);
            }
            else
            {
                return await ProcessVariableAsync(placeholder, sessionId, token);
            }
        }

        // Routes to the correct method implementation
        private async Task<string> ProcessMethodAsync(string placeholder, string sessionId, CancellationToken token)
        {
            int openParenIndex = placeholder.IndexOf('(');
            string functionName = placeholder.Substring(0, openParenIndex).Trim();
            string parameters = placeholder.Substring(openParenIndex + 1, placeholder.Length - openParenIndex - 2).Trim();
            return functionName.ToLowerInvariant() switch
            {
                // Basic
                "random"                 => await GenerateRandomStringAsync(parameters, sessionId, token),
                "randomnumber"           => await GenerateRandomNumberAsync(parameters, sessionId, token),
                "timestamp" or "datetime"=> await GenerateTimestampAsync(parameters, sessionId, token),
                "guid"                   => await GenerateGuidAsync(parameters, sessionId, token),
                "loopcounter" or "iterate"=> await IterateAsync(parameters, sessionId, token),
                "uuid"                   => await GenerateUuidAsync(parameters, sessionId, token),

                // Encoding
                "urlencode"              => await UrlEncodeAsync(parameters, sessionId, token),
                "urldecode"              => await UrlDecodeAsync(parameters, sessionId, token),
                "base64encode"           => await Base64EncodeAsync(parameters, sessionId, token),
                "base64decode"           => await Base64DecodeAsync(parameters, sessionId, token),

                // Security
                "hash"                   => await GenerateHashAsync(parameters, sessionId, token),
                "jwtclaim"               => await ExtractJwtClaimAsync(parameters, sessionId, token),

                // Text
                "format"                 => await FormatTemplateAsync(parameters, sessionId, token),
                "generateemail"          => await GenerateEmailAsync(parameters, sessionId, token),

                // IO
                "read"                   => await ReadFileAsync(parameters, sessionId, token),

                _ => await ProcessVariableAsync(functionName, sessionId, token)
            };
        }

        // Shared helpers
        // Helpers (local/private). Keep logs concise and avoid dumping huge/secret values.
        private static string TruncateForLog(string? value, int max = 128)
            => string.IsNullOrEmpty(value) ? "<empty>" : (value.Length <= max ? value : value.Substring(0, max) + $"...(+{value.Length - max} chars)");

        private static string MaskJwtForLog(string? jwt)
        {
            if (string.IsNullOrWhiteSpace(jwt)) return "<empty>";
            var parts = jwt.Split('.');
            var head = parts.Length > 0 ? parts[0] : "";
            var payload = parts.Length > 1 ? parts[1] : "";
            return $"header({TruncateForLog(head, 32)}), payload({TruncateForLog(payload, 32)}), sig({(parts.Length > 2 ? "<present>" : "<missing>")})";
        }


        private async Task StoreVariableIfNeededAsync(string variableName, string value, CancellationToken token)
        {
            if (string.IsNullOrWhiteSpace(variableName))
                return;

            var holder = await new StringVariableHolder.VBuilder(_placeholderResolverService, _logger, _runtimeOperationIdProvider)
                .WithType(VariableType.String)
                .WithRawValue(value)
                .SetGlobal()
                .BuildAsync(token);

            await _variableManager.PutAsync(variableName, holder, token);
        }
    }
}
