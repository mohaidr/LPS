
using System;
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

        protected static string Truncate(string? value, int max = 128) =>
            string.IsNullOrEmpty(value) ? "<empty>" :
            value.Length <= max ? value : value.Substring(0, max) + $"...(+{value.Length - max})";

        protected static string PadBase64(string s) =>
            string.IsNullOrEmpty(s) ? s : s.PadRight(s.Length + (4 - s.Length % 4) % 4, '=');
    }
}
