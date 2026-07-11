
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using LPS.Domain.Common;
using LPS.Domain.Common.Interfaces;
using LPS.Domain.Domain.Common.Enums;
using LPS.Infrastructure.VariableServices.GlobalVariableManager;
using LPS.Infrastructure.VariableServices.VariableHolders;

namespace LPS.Infrastructure.PlaceHolderService.Methods
{
    public abstract class MethodBase : IPlaceholderMethod
    {
        protected readonly ParameterExtractorService _params;
        protected readonly ILogger _logger;
        protected readonly IRuntimeOperationIdProvider _op;
        protected readonly IVariableManager _variables;
        protected readonly Lazy<IPlaceholderResolverService> _resolver; // Use Lazy

        protected MethodBase(
            ParameterExtractorService @params,
            ILogger logger,
            IRuntimeOperationIdProvider op,
            IVariableManager variables,
            Lazy<IPlaceholderResolverService> resolver) // Inject Lazy
        {
            _params = @params;
            _logger = logger;
            _op = op;
            _variables = variables;
            _resolver = resolver;
        }

        public abstract string Name { get; }
        public abstract Task<string> ExecuteAsync(string parameters, string sessionId, CancellationToken token);

        protected async Task StoreVariableIfNeededAsync(string variableName, string value, CancellationToken token)
        {
            if (string.IsNullOrWhiteSpace(variableName)) return;

            var holder = await new StringVariableHolder.VMaintainer(_resolver.Value, _logger, _op) // Use .Value
                .WithType(VariableType.String)
                .WithRawValue(value ?? string.Empty)
                .SetGlobal()
                .UpdateAsync(token);

            await _variables.PutAsync(variableName, holder, token);
        }

        /// <summary>
        /// Stores a value under <paramref name="variableName"/> using the variable type that best
        /// matches the JSON token (or an explicit <paramref name="asType"/> override), so downstream
        /// placeholders can path-navigate objects/arrays or use numbers/booleans naturally.
        /// </summary>
        protected async Task StoreTypedVariableAsync(string variableName, JToken value, string asType, CancellationToken token)
        {
            if (string.IsNullOrWhiteSpace(variableName)) return;

            value ??= JValue.CreateNull();
            var targetType = MapAsType(asType) ?? InferVariableType(value);
            var holder = await BuildTypedHolderAsync(targetType, value, token);
            await _variables.PutAsync(variableName, holder, token);
        }

        private async Task<IVariableHolder> BuildTypedHolderAsync(VariableType targetType, JToken value, CancellationToken token)
        {
            switch (targetType)
            {
                case VariableType.Boolean:
                    if (TryGetBool(value, out var b))
                        return await new BooleanVariableHolder.VMaintainer(_logger, _op)
                            .WithRawValue(b).SetGlobal().UpdateAsync(token);
                    break;

                case VariableType.Int:
                    if (TryGetLong(value, out var l) && l >= int.MinValue && l <= int.MaxValue)
                        return await new NumberVariableHolder.VMaintainer(_logger, _op)
                            .WithRawValue((int)l).SetGlobal().UpdateAsync(token);
                    if (TryGetDouble(value, out var di))
                        return await new NumberVariableHolder.VMaintainer(_logger, _op)
                            .WithRawValue(di).SetGlobal().UpdateAsync(token);
                    break;

                case VariableType.Double:
                case VariableType.Float:
                    if (TryGetDouble(value, out var d))
                        return await new NumberVariableHolder.VMaintainer(_logger, _op)
                            .WithRawValue(d).SetGlobal().UpdateAsync(token);
                    break;

                case VariableType.Decimal:
                    if (TryGetDecimal(value, out var m))
                        return await new NumberVariableHolder.VMaintainer(_logger, _op)
                            .WithRawValue(m).SetGlobal().UpdateAsync(token);
                    break;

                case VariableType.JsonString:
                    return await new StringVariableHolder.VMaintainer(_resolver.Value, _logger, _op)
                        .WithType(VariableType.JsonString).WithRawValue(JsonRaw(value)).SetGlobal().UpdateAsync(token);
            }

            // Fallback: store as a plain string.
            return await new StringVariableHolder.VMaintainer(_resolver.Value, _logger, _op)
                .WithType(VariableType.String).WithRawValue(ScalarRaw(value)).SetGlobal().UpdateAsync(token);
        }

        private static VariableType? MapAsType(string asType) =>
            (asType?.Trim().ToLowerInvariant()) switch
            {
                "json" or "jsonstring" => VariableType.JsonString,
                "int" or "integer" => VariableType.Int,
                "double" or "float" or "number" => VariableType.Double,
                "decimal" => VariableType.Decimal,
                "bool" or "boolean" => VariableType.Boolean,
                "string" or "text" => VariableType.String,
                _ => (VariableType?)null // auto / empty / unknown -> infer
            };

        private static VariableType InferVariableType(JToken value) =>
            value.Type switch
            {
                JTokenType.Object or JTokenType.Array => VariableType.JsonString,
                JTokenType.Integer => VariableType.Int,
                JTokenType.Float => VariableType.Double,
                JTokenType.Boolean => VariableType.Boolean,
                _ => VariableType.String
            };

        private static string JsonRaw(JToken value) =>
            value.Type is JTokenType.Object or JTokenType.Array
                ? value.ToString(Formatting.None)
                : ScalarRaw(value);

        private static string ScalarRaw(JToken v) =>
            v.Type switch
            {
                JTokenType.Null => string.Empty,
                JTokenType.String => v.Value<string>() ?? string.Empty,
                JTokenType.Boolean => v.Value<bool>() ? "true" : "false",
                JTokenType.Integer => v.Value<long>().ToString(CultureInfo.InvariantCulture),
                JTokenType.Float => v.Value<double>().ToString(CultureInfo.InvariantCulture),
                JTokenType.Object or JTokenType.Array => v.ToString(Formatting.None),
                _ => v.ToString()
            };

        private static bool TryGetBool(JToken v, out bool result)
        {
            if (v.Type == JTokenType.Boolean) { result = v.Value<bool>(); return true; }
            return bool.TryParse(ScalarRaw(v), out result);
        }

        private static bool TryGetLong(JToken v, out long result)
        {
            if (v.Type == JTokenType.Integer) { result = v.Value<long>(); return true; }
            return long.TryParse(ScalarRaw(v), NumberStyles.Integer, CultureInfo.InvariantCulture, out result);
        }

        private static bool TryGetDouble(JToken v, out double result)
        {
            if (v.Type is JTokenType.Integer or JTokenType.Float) { result = v.Value<double>(); return true; }
            return double.TryParse(ScalarRaw(v), NumberStyles.Float, CultureInfo.InvariantCulture, out result);
        }

        private static bool TryGetDecimal(JToken v, out decimal result)
        {
            if (v.Type is JTokenType.Integer or JTokenType.Float) { result = v.Value<decimal>(); return true; }
            return decimal.TryParse(ScalarRaw(v), NumberStyles.Number, CultureInfo.InvariantCulture, out result);
        }

        // ---- Numeric helpers shared by the arithmetic / comparison declarative methods ----

        /// <summary>
        /// Resolves a single named numeric parameter. When <paramref name="allowPositional"/> is true
        /// and the key is absent, falls back to the first positional (unnamed) argument.
        /// Returns null when the value is missing or not a number.
        /// </summary>
        protected async Task<double?> ResolveNumberParamAsync(string parameters, string key, string sessionId, CancellationToken token, bool allowPositional = false)
        {
            var raw = await _params.ExtractStringAsync(parameters, key, null, sessionId, token);
            if (raw == null && allowPositional)
            {
                var positional = FirstPositionalArgument(parameters);
                if (!string.IsNullOrWhiteSpace(positional))
                    raw = await _resolver.Value.ResolvePlaceholdersAsync<string>(positional, sessionId, token);
            }

            return TryParseNumber(raw, out var value) ? value : (double?)null;
        }

        /// <summary>
        /// Resolves a numeric list from a <c>source</c> (or supplied key) parameter. The source may be a
        /// JSON array (<c>[${x}, 10, 4, ${z}]</c>), a variable that resolves to a JSON array, or a single
        /// scalar. Non-numeric elements are skipped and reported.
        /// </summary>
        protected async Task<List<double>> ResolveNumberArrayAsync(string parameters, string sessionId, CancellationToken token, string key = "source")
        {
            var values = new List<double>();

            var raw = await _params.ExtractStringAsync(parameters, key, null, sessionId, token);
            if (raw == null)
            {
                var positional = FirstPositionalArgument(parameters);
                if (!string.IsNullOrWhiteSpace(positional))
                    raw = await _resolver.Value.ResolvePlaceholdersAsync<string>(positional, sessionId, token);
            }

            if (string.IsNullOrWhiteSpace(raw))
                return values;

            raw = raw.Trim();
            int skipped = 0;

            if (raw.StartsWith("[") && raw.EndsWith("]"))
            {
                JArray array = null;
                try { array = JArray.Parse(raw); } catch { array = null; }

                if (array != null)
                {
                    foreach (var element in array)
                    {
                        if (TryTokenToNumber(element, out var d)) values.Add(d);
                        else skipped++;
                    }
                }
                else
                {
                    // Fallback for non-JSON content such as [abc, 10] where elements are unquoted.
                    var inner = raw.Substring(1, raw.Length - 2);
                    foreach (var part in inner.Split(','))
                    {
                        var trimmed = part.Trim().Trim('"');
                        if (trimmed.Length == 0) continue;
                        if (TryParseNumber(trimmed, out var d)) values.Add(d);
                        else skipped++;
                    }
                }
            }
            else if (TryParseNumber(raw, out var single))
            {
                values.Add(single);
            }
            else
            {
                skipped++;
            }

            if (skipped > 0)
                await _logger.LogAsync(_op.OperationId, $"{skipped} non-numeric value(s) were skipped while parsing '{key}'.", LPSLoggingLevel.Warning, token);

            return values;
        }

        /// <summary>
        /// Stores a numeric result (typed) under <paramref name="variableName"/> when provided and returns
        /// its string form. Whole values are emitted as integers unless <paramref name="forceDouble"/> is set;
        /// an explicit <paramref name="asType"/> override is honored by the typed store.
        /// </summary>
        protected async Task<string> StoreNumberResultAsync(string variableName, double value, string asType, CancellationToken token, bool forceDouble = false)
        {
            bool integral = !forceDouble
                && !double.IsNaN(value) && !double.IsInfinity(value)
                && value == Math.Floor(value)
                && value >= long.MinValue && value <= long.MaxValue;

            JToken jt;
            string text;
            if (integral)
            {
                long asLong = (long)value;
                jt = new JValue(asLong);
                text = asLong.ToString(CultureInfo.InvariantCulture);
            }
            else
            {
                jt = new JValue(value);
                text = value.ToString(CultureInfo.InvariantCulture);
            }

            if (!string.IsNullOrWhiteSpace(variableName))
                await StoreTypedVariableAsync(variableName, jt, asType, token);

            return text;
        }

        /// <summary>Logs a numeric-method failure, stores an empty result, and returns an empty string.</summary>
        protected async Task<string> FailNumberAsync(string variableName, string methodName, CancellationToken token, string reason = "No numeric values were provided.")
        {
            await _logger.LogAsync(_op.OperationId, $"{methodName} failed. {reason}", LPSLoggingLevel.Warning, token);
            await StoreVariableIfNeededAsync(variableName, string.Empty, token);
            return string.Empty;
        }

        protected static bool TryParseNumber(string value, out double result) =>
            double.TryParse((value ?? string.Empty).Trim(), NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out result);

        private static bool TryTokenToNumber(JToken element, out double result)
        {
            switch (element.Type)
            {
                case JTokenType.Integer:
                case JTokenType.Float:
                    result = element.Value<double>();
                    return true;
                case JTokenType.String:
                    return TryParseNumber(element.Value<string>(), out result);
                default:
                    result = 0;
                    return false;
            }
        }

        /// <summary>Returns the first positional (unnamed) argument, or empty when none exists.</summary>
        protected static string FirstPositionalArgument(string parameters)
        {
            if (string.IsNullOrWhiteSpace(parameters))
                return string.Empty;

            foreach (var part in parameters.Split(','))
            {
                var trimmed = part.Trim();
                if (!string.IsNullOrWhiteSpace(trimmed) && !trimmed.Contains('='))
                    return trimmed;
            }

            return string.Empty;
        }

        protected static string Truncate(string? value, int max = 128) =>
            string.IsNullOrEmpty(value) ? "<empty>" :
            value.Length <= max ? value : value.Substring(0, max) + $"...(+{value.Length - max})";

        protected static string PadBase64(string s) =>
            string.IsNullOrEmpty(s) ? s : s.PadRight(s.Length + (4 - s.Length % 4) % 4, '=');
    }
}
