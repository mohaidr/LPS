
using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using LPS.Domain.Common;
using LPS.Domain.Common.Interfaces;
using LPS.Infrastructure.VariableServices.GlobalVariableManager;

namespace LPS.Infrastructure.PlaceHolderService.Methods
{
    public sealed class JwtClaimMethod : MethodBase
    {
        public JwtClaimMethod(ParameterExtractorService p, ILogger l, IRuntimeOperationIdProvider op, IVariableManager v, Lazy<IPlaceholderResolverService> r)
            : base(p,l,op,v,r) { }

        public override string Name => "jwtclaim";

        public override async Task<string> ExecuteAsync(string parameters, string sessionId, CancellationToken token)
        {
            string variableName = string.Empty;
            string tokenStr = string.Empty;
            string claim = string.Empty;
            try
            {
                tokenStr = await _params.ExtractStringAsync(parameters, "token", "", sessionId, token);
                claim = await _params.ExtractStringAsync(parameters, "claim", "", sessionId, token);
                variableName = await _params.ExtractStringAsync(parameters, "variable", "", sessionId, token);

                if (string.IsNullOrEmpty(tokenStr) || string.IsNullOrEmpty(claim))
                {
                    await StoreVariableIfNeededAsync(variableName, string.Empty, token);
                    return string.Empty;
                }

                string[] parts = tokenStr.Split('.');
                if (parts.Length < 2)
                {
                    await _logger.LogAsync(_op.OperationId, "jwtclaim: invalid token format.", LPSLoggingLevel.Error, token);
                    await StoreVariableIfNeededAsync(variableName, string.Empty, token);
                    return string.Empty;
                }

                string payloadJson = Encoding.UTF8.GetString(Convert.FromBase64String(PadBase64(parts[1])));
                var dict = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(payloadJson);

                string result = (dict != null && dict.TryGetValue(claim, out var valueObj)) ? valueObj?.ToString() ?? string.Empty : string.Empty;
                await StoreVariableIfNeededAsync(variableName, result, token);
                return result;
            }
            catch (Exception ex)
            {
                await _logger.LogAsync(_op.OperationId, $"jwtclaim failed. {ex}", LPSLoggingLevel.Error, token);
                await StoreVariableIfNeededAsync(variableName, string.Empty, token);
                return string.Empty;
            }
        }
    }
}
