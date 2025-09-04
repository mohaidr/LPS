
using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using LPS.Domain.Common;
using LPS.Domain.Common.Interfaces;
using LPS.Infrastructure.VariableServices.GlobalVariableManager;

namespace LPS.Infrastructure.PlaceHolderService.Methods
{
    public sealed class Base64EncodeMethod : MethodBase
    {
        public Base64EncodeMethod(ParameterExtractorService p, ILogger l, IRuntimeOperationIdProvider op, IVariableManager v, Lazy<IPlaceholderResolverService> r)
            : base(p,l,op,v,r) { }

        public override string Name => "base64encode";

        public override async Task<string> ExecuteAsync(string parameters, string sessionId, CancellationToken token)
        {
            string variableName = string.Empty, value = string.Empty;
            try
            {
                value = await _params.ExtractStringAsync(parameters, "value", string.Empty, sessionId, token);
                variableName = await _params.ExtractStringAsync(parameters, "variable", "", sessionId, token);
                string result = string.IsNullOrEmpty(value) ? string.Empty : Convert.ToBase64String(Encoding.UTF8.GetBytes(value));
                await StoreVariableIfNeededAsync(variableName, result, token);
                return result;
            }
            catch (Exception ex)
            {
                await _logger.LogAsync(_op.OperationId, $"base64encode failed for '{Truncate(value)}'. {ex}", LPSLoggingLevel.Error, token);
                await StoreVariableIfNeededAsync(variableName, string.Empty, token);
                return string.Empty;
            }
        }
    }
}
