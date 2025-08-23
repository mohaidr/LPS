using LPS.Domain.Common.Interfaces;
using System;
using System.Threading;
using System.Threading.Tasks;
using NCalc;
using System.Linq.Expressions;
using LPS.Domain.Domain.Common.Interfaces;
using LPS.Infrastructure.Nodes;

namespace LPS.Infrastructure.Skip
{
    public class SkipIfEvaluator: ISkipIfEvaluator
    {
        private readonly IPlaceholderResolverService _placeholderResolver;
        private readonly ILogger _logger;
        IRuntimeOperationIdProvider _runtimeOperationIdProvider;
        INodeMetadata _nodeMetadata;
        public SkipIfEvaluator(IPlaceholderResolverService placeholderResolver, INodeMetadata nodeMetadata, IRuntimeOperationIdProvider runtimeOperationIdProvider, ILogger logger)
        {
            _placeholderResolver = placeholderResolver;
            _logger = logger;
            _runtimeOperationIdProvider = runtimeOperationIdProvider;
            _nodeMetadata = nodeMetadata;
        }

        //TODO: This must be smarter in the future, detect the string variable holders and ask to return them as quoted for correct comparison insted of forcing the user to define the variable as Q type
        // This can happen by updating the placeholder to offer an option to ask for the value as qouted value if the variable holder it detected is string
        public async Task<bool> ShouldSkipAsync(string skipIfExpression, string sessionId, CancellationToken token)
        {

            if (string.IsNullOrWhiteSpace(skipIfExpression))
                return false;

            try
            {
                // Resolve placeholders first
                var resolved = await _placeholderResolver.ResolvePlaceholdersAsync<string>(skipIfExpression, sessionId, token);

                if (string.IsNullOrWhiteSpace(resolved))
                    return false;

                // Evaluate the resolved expression using NCalcAsync
                var expr = new AsyncExpression(resolved, ExpressionOptions.StrictTypeMatching);
                var result = await expr.EvaluateAsync();

                if (result is bool skip && skip)
                {
                    await _logger.LogAsync(
                        _runtimeOperationIdProvider.OperationId,
                        $"Iteration skipped due to evaluated condition: {resolved}. Node Details (Name: {_nodeMetadata.NodeName}, IP: {_nodeMetadata.NodeIP})",
                        LPSLoggingLevel.Warning);

                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                await _logger.LogAsync(_runtimeOperationIdProvider.OperationId, $"Failed to evaluate skipIf condition: {skipIfExpression} \n{ex}", LPSLoggingLevel.Error);
                return false;
            }
        }

        public  bool IsValidExpression(string expressionText)
        {
            try
            {
                var expr = new AsyncExpression(expressionText, ExpressionOptions.AllowNullOrEmptyExpressions);
                expr.EvaluateAsync();
                return true;
            }
            catch(Exception ex) 
            {
                Console.WriteLine(ex.ToString());
                return false;
            }
        }

    }
}
