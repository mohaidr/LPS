
using LPS.Domain.Common.Interfaces;
using LPS.Domain.Common;
using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using LPS.Infrastructure.VariableServices.VariableHolders;

namespace LPS.Infrastructure.VariableServices.PlaceHolderService
{
    internal partial class PlaceholderProcessor
    {
        private async Task<string> ProcessVariableAsync(string placeholder, string sessionId, CancellationToken token)
        {
            string variableName = placeholder;
            string? path = null;

            if (placeholder.Contains('.') || placeholder.Contains('/') || placeholder.Contains('['))
            {
                int splitIndex = placeholder.IndexOfAny(new[] {'.', '/', '['});
                variableName = placeholder.Substring(0, splitIndex);
                path = placeholder.Substring(splitIndex);
            }

            var variableHolder = await _sessionManager.GetVariableAsync(sessionId, variableName, token)
                                 ?? await _variableManager.GetAsync(variableName, token);

            if (variableHolder == null)
            {
                await _logger.LogAsync(_runtimeOperationIdProvider.OperationId, $"Variable '{variableName}' not found.", LPSLoggingLevel.Warning, token);
                return $"${{{variableName}{path}}}";
            }

            if (string.IsNullOrWhiteSpace(path))
            {
                return await variableHolder.GetRawValueAsync(token);
            }
            else if (variableHolder is IObjectVariableHolder objHolder)
            {
                return await objHolder.GetValueAsync(path, sessionId, token);
            }
            else
            {
                return await variableHolder.GetRawValueAsync(token);
            }
        }
    }
}
