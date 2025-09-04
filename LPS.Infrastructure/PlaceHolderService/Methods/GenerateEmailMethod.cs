
using System;
using System.Threading;
using System.Threading.Tasks;
using LPS.Domain.Common;
using LPS.Domain.Common.Interfaces;
using LPS.Infrastructure.VariableServices.GlobalVariableManager;

namespace LPS.Infrastructure.PlaceHolderService.Methods
{
    public sealed class GenerateEmailMethod : MethodBase
    {
        public GenerateEmailMethod(ParameterExtractorService p, ILogger l, IRuntimeOperationIdProvider op, IVariableManager v, Lazy<IPlaceholderResolverService> r)
            : base(p,l,op,v,r) { }

        public override string Name => "generateemail";

        public override async Task<string> ExecuteAsync(string parameters, string sessionId, CancellationToken token)
        {
            string variableName = string.Empty;
            try
            {
                string prefix = await _params.ExtractStringAsync(parameters, "prefix", "user", sessionId, token);
                string domain = await _params.ExtractStringAsync(parameters, "domain", "example.com", sessionId, token);
                variableName = await _params.ExtractStringAsync(parameters, "variable", "", sessionId, token);
                string unique = Guid.NewGuid().ToString("N").Substring(0, 8);
                string email = $"{prefix}-{unique}@{domain}";
                await StoreVariableIfNeededAsync(variableName, email, token);
                return email;
            }
            catch (Exception ex)
            {
                await _logger.LogAsync(_op.OperationId, $"generateemail failed. {ex}", LPSLoggingLevel.Error, token);
                await StoreVariableIfNeededAsync(variableName, string.Empty, token);
                return string.Empty;
            }
        }
    }
}
