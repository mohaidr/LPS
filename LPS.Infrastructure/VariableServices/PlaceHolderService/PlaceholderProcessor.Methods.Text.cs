
using LPS.Domain.Common.Interfaces;
using LPS.Infrastructure.Common;
using LPS.Infrastructure.LPSClients.CachService;
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace LPS.Infrastructure.VariableServices.PlaceHolderService
{
    internal partial class PlaceholderProcessor
    {
        private async Task<string> FormatTemplateAsync(string parameters, string sessionId, CancellationToken token)
        {
            string variableName = string.Empty;
            string template = string.Empty;
            string args = string.Empty;
            try
            {
                template = await _paramService.ExtractStringAsync(parameters, "template", string.Empty, sessionId, token);
                args = await _paramService.ExtractStringAsync(parameters, "args", string.Empty, sessionId, token);
                variableName = await _paramService.ExtractStringAsync(parameters, "variable", "", sessionId, token);

                string result = string.Format(template, args.Split(",").ToArray());
                result = await _placeholderResolverService.ResolvePlaceholdersAsync<string>(result, string.Empty, token);

                await StoreVariableIfNeededAsync(variableName, result, token);
                return result;
            }
            catch (Exception ex)
            {
                await _logger.LogAsync(_runtimeOperationIdProvider.OperationId, $"Failed FormatTemplateAsync. template='{template}', args='{args}'. {ex}", LPSLoggingLevel.Error, token);
                await StoreVariableIfNeededAsync(variableName, string.Empty, token);
                return string.Empty;
            }
        }

        private async Task<string> GenerateEmailAsync(string parameters, string sessionId, CancellationToken token)
        {
            string variableName = string.Empty;
            try
            {
                string prefix = await _paramService.ExtractStringAsync(parameters, "prefix", "user", sessionId, token);
                string domain = await _paramService.ExtractStringAsync(parameters, "domain", "example.com", sessionId, token);
                variableName = await _paramService.ExtractStringAsync(parameters, "variable", "", sessionId, token);
                string uniquePart = Guid.NewGuid().ToString("N").Substring(0, 8);
                string email = $"{prefix}-{uniquePart}@{domain}";
                await StoreVariableIfNeededAsync(variableName, email, token);
                return email;
            }
            catch (Exception ex)
            {
                await _logger.LogAsync(_runtimeOperationIdProvider.OperationId, $"Failed GenerateEmailAsync (prefix/domain). {ex}", LPSLoggingLevel.Error, token);
                await StoreVariableIfNeededAsync(variableName, string.Empty, token);
                return string.Empty;
            }
        }

    }
}
