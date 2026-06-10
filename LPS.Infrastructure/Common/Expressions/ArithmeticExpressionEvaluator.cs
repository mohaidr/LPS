using Flee.PublicTypes;
using System;
using System.Collections.Concurrent;
using System.Globalization;
using System.Text.RegularExpressions;

namespace LPS.Infrastructure.Common.Expressions
{
    public static class ArithmeticExpressionEvaluator
    {
        private static readonly ConcurrentDictionary<string, IGenericExpression<int>> IntExpressionCache = new(StringComparer.Ordinal);
        private static readonly ConcurrentDictionary<string, IGenericExpression<double>> DoubleExpressionCache = new(StringComparer.Ordinal);
        private static readonly Regex ArithmeticExpressionRegex = new(@"^[0-9\s\.\+\-\*\/\(\)]+$", RegexOptions.Compiled);
        private const int MaxExpressionCacheSize = 1024;

        public static bool IsArithmeticExpression(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            return ArithmeticExpressionRegex.IsMatch(value.Trim());
        }

        public static bool TryEvaluateToInt(string expression, out int result)
        {
            result = default;
            if (!IsArithmeticExpression(expression))
            {
                return false;
            }

            var candidate = expression.Trim();

            try
            {
                try
                {
                    var intExpr = IntExpressionCache.GetOrAdd(candidate, static expr =>
                    {
                        var ctx = CreateExpressionContext();
                        return ctx.CompileGeneric<int>(expr);
                    });
                    result = intExpr.Evaluate();
                    return true;
                }
                catch
                {
                    var doubleExpr = DoubleExpressionCache.GetOrAdd(candidate, static expr =>
                    {
                        var ctx = CreateExpressionContext();
                        return ctx.CompileGeneric<double>(expr);
                    });
                    var computed = doubleExpr.Evaluate();
                    result = Convert.ToInt32(computed, CultureInfo.InvariantCulture);
                    return true;
                }
            }
            catch
            {
                return false;
            }
            finally
            {
                TrimExpressionCachesIfNeeded();
            }
        }

        private static ExpressionContext CreateExpressionContext()
        {
            var ctx = new ExpressionContext();
            ctx.Options.ParseCulture = CultureInfo.InvariantCulture;
            ctx.Options.Checked = true;
            ctx.Imports.AddType(typeof(Math));
            return ctx;
        }

        private static void TrimExpressionCachesIfNeeded()
        {
            if (IntExpressionCache.Count > MaxExpressionCacheSize)
            {
                IntExpressionCache.Clear();
            }

            if (DoubleExpressionCache.Count > MaxExpressionCacheSize)
            {
                DoubleExpressionCache.Clear();
            }
        }
    }
}