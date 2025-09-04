
using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using LPS.Domain.Common;
using LPS.Domain.Common.Interfaces;
using LPS.Infrastructure.PlaceHolderService;
using LPS.Infrastructure.VariableServices.GlobalVariableManager;

namespace LPS.Infrastructure.PlaceHolderService.Methods
{
    public sealed class Base64DecodeMethod : MethodBase
    {
        public Base64DecodeMethod(ParameterExtractorService p, ILogger l, IRuntimeOperationIdProvider op, IVariableManager v, Lazy<IPlaceholderResolverService> r)
            : base(p,l,op,v,r) { }

        public override string Name => "base64decode";

        public override async Task<string> ExecuteAsync(string parameters, string sessionId, CancellationToken token)
        {
            string variableName = string.Empty, value = string.Empty;
            try
            {
                value = await _params.ExtractStringAsync(parameters, "value", string.Empty, sessionId, token);
                variableName = await _params.ExtractStringAsync(parameters, "variable", "", sessionId, token);

                if (string.IsNullOrEmpty(value))
                {
                    await StoreVariableIfNeededAsync(variableName, string.Empty, token);
                    return string.Empty;
                }

                string padded = PadBase64(value);
                string result = Encoding.UTF8.GetString(Convert.FromBase64String(padded));
                await StoreVariableIfNeededAsync(variableName, result, token);
                return result;
            }
            catch (Exception ex)
            {
                await _logger.LogAsync(_op.OperationId, $"base64decode failed for '{Truncate(value)}'. {ex}", LPSLoggingLevel.Error, token);
                await StoreVariableIfNeededAsync(variableName, string.Empty, token);
                return string.Empty;
            }
        }
    }
}
