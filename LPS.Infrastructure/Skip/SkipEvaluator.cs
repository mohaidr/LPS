//using LPS.Domain.Common.Interfaces;
//using NCalcAsync;
//using System;
//using System.Linq.Expressions;
//using System.Threading;
//using System.Threading.Tasks;

//namespace LPS.Infrastructure.Skip
//{
//    public class SkipIfEvaluator
//    {
//        private readonly IPlaceholderResolverService _placeholderResolver;
//        private readonly ILogger _logger;

//        public SkipIfEvaluator(IPlaceholderResolverService placeholderResolver, ILogger logger)
//        {
//            _placeholderResolver = placeholderResolver;
//            _logger = logger;
//        }

//        public async Task<bool> ShouldSkipAsync(string? skipIfExpression, string sessionId, CancellationToken token)
//        {
//            if (string.IsNullOrWhiteSpace(skipIfExpression))
//                return false;

//            try
//            {
//                // Resolve placeholders first
//                var resolved = await _placeholderResolver.ResolvePlaceholdersAsync<string>(skipIfExpression, sessionId, token);

//                if (string.IsNullOrWhiteSpace(resolved))
//                    return false;

//                // Evaluate the resolved expression using NCalcAsync
//                var expr = new Expression(resolved);
//                var result = await expr.EvaluateAsync();

//                if (result is bool skip && skip)
//                {
//                    _logger.LogInformation($"Iteration skipped due to evaluated condition: {resolved}");
//                    return true;
//                }

//                return false;
//            }
//            catch (Exception ex)
//            {
//                _logger.LogWarning(ex, $"Failed to evaluate skipIf condition: {skipIfExpression}");
//                return false;
//            }
//        }
//    }
//}
