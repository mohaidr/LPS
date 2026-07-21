using LPS.Domain.Common;
using LPS.Domain.Common.Interfaces;
using LPS.Domain.Domain.Common.Enums;
using LPS.Infrastructure.Common.Interfaces;
using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;

namespace LPS.Infrastructure.VariableServices.VariableHolders
{
    public class NumberVariableHolder : IVariableHolder
    {

        public string Value { get; private set; }
        public VariableType? Type { get; private set; }
        public bool IsGlobal { get; private set; }

        // Natural CLR value (boxed once), aligned with Type (int/double/float/decimal).
        // Value (string) remains the canonical resolved form used by GetRawValueAsync; this is an
        // additive typed view so typed consumers (math/comparison) don't have to re-parse the string.
        private object _number;

        private readonly ILogger _logger;
        private readonly IRuntimeOperationIdProvider _runtimeOperationIdProvider;
        private readonly VMaintainer _maintainer;
        public IVariableMaintainer Maintainer => _maintainer;

        private NumberVariableHolder(
            ILogger logger,
            IRuntimeOperationIdProvider runtimeOperationIdProvider, VMaintainer maintainer)
        {

            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _runtimeOperationIdProvider = runtimeOperationIdProvider ?? throw new ArgumentNullException(nameof(runtimeOperationIdProvider));
            _maintainer = maintainer ?? throw new ArgumentNullException(nameof(maintainer));
        }

        public ValueTask<string> GetRawValueAsync(CancellationToken token) => ValueTask.FromResult(Value);

        // Typed views of the stored number (off the resolution hot path). Numeric-to-numeric
        // conversions ignore culture; the invariant provider is passed only for consistency.
        public long AsInt64() => Convert.ToInt64(_number, CultureInfo.InvariantCulture);
        public double AsDouble() => Convert.ToDouble(_number, CultureInfo.InvariantCulture);
        public decimal AsDecimal() => Convert.ToDecimal(_number, CultureInfo.InvariantCulture);

        // ====== Builder ======
        //This is a one one builder which will always return the same variable holder. If used in the intent of creating multiple different holders, this will result on logical errors
        public sealed class VMaintainer : IVariableMaintainer
        {
            private readonly NumberVariableHolder _variableHolder;
            private readonly ILogger _logger;
            private readonly IRuntimeOperationIdProvider _runtimeOperationIdProvider;

            // Local buffered state (do NOT touch _variableHolder until BuildAsync)
            private string _rawValue;
            private object _boxedValue; // natural CLR value, boxed once, mirrors _rawValue/_type
            private VariableType _type = VariableType.Int; // default fallback
            private bool _isGlobal;

            public VMaintainer(
                ILogger logger,
                IRuntimeOperationIdProvider runtimeOperationIdProvider)
            {
                _logger = logger ?? throw new ArgumentNullException(nameof(logger));
                _runtimeOperationIdProvider = runtimeOperationIdProvider ?? throw new ArgumentNullException(nameof(runtimeOperationIdProvider));

                // Pre-create holder; assign in BuildAsync
                _variableHolder = new NumberVariableHolder(_logger, _runtimeOperationIdProvider, this)
                {
                    Type = VariableType.Int // default fallback
                };
            }

            // Buffer numeric overloads locally (string + explicit VariableType)
            public VMaintainer WithRawValue(int value)
            {
                _rawValue = value.ToString(System.Globalization.CultureInfo.InvariantCulture);
                _boxedValue = value;
                _type = VariableType.Int;
                return this;
            }

            public VMaintainer WithRawValue(double value)
            {
                _rawValue = value.ToString(System.Globalization.CultureInfo.InvariantCulture);
                _boxedValue = value;
                _type = VariableType.Double;
                return this;
            }

            public VMaintainer WithRawValue(float value)
            {
                _rawValue = value.ToString(System.Globalization.CultureInfo.InvariantCulture);
                _boxedValue = value;
                _type = VariableType.Float;
                return this;
            }

            public VMaintainer WithRawValue(decimal value)
            {
                _rawValue = value.ToString(System.Globalization.CultureInfo.InvariantCulture);
                _boxedValue = value;
                _type = VariableType.Decimal;
                return this;
            }

            public VMaintainer SetGlobal(bool isGlobal = true)
            {
                _isGlobal = isGlobal; // buffer locally
                return this;
            }

            public async ValueTask<IVariableHolder> UpdateAsync(CancellationToken token)
            {
                token.ThrowIfCancellationRequested();

                // Validate buffered state
                if (string.IsNullOrWhiteSpace(_rawValue))
                {
                    await _logger.LogAsync(
                        _runtimeOperationIdProvider.OperationId,
                        "The raw value of the holder can't be null or empty.",
                        LPSLoggingLevel.Error,
                        token);

                    throw new InvalidOperationException("The raw value of the holder can't be null or empty.");
                }

                // Assign buffered values atomically to the pre-created holder
                _variableHolder.Value = _rawValue;
                _variableHolder._number = _boxedValue;
                _variableHolder.Type = _type;
                _variableHolder.IsGlobal = _isGlobal;

                return _variableHolder;
            }
        }
    }
}
