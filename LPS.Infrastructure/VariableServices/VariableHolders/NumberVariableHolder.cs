using LPS.Domain.Common;
using LPS.Domain.Common.Interfaces;
using LPS.Domain.Domain.Common.Enums;
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
        private readonly ILogger _logger;
        private readonly IRuntimeOperationIdProvider _runtimeOperationIdProvider;

        private NumberVariableHolder(
            ILogger logger,
            IRuntimeOperationIdProvider runtimeOperationIdProvider)
        {

            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _runtimeOperationIdProvider = runtimeOperationIdProvider ?? throw new ArgumentNullException(nameof(runtimeOperationIdProvider));
        }

        public ValueTask<string> GetRawValueAsync() => ValueTask.FromResult(Value);

        // ====== Builder ======
        public class Builder
        {
            private readonly NumberVariableHolder _variableHolder;
            private readonly ILogger _logger;
            private readonly IRuntimeOperationIdProvider _runtimeOperationIdProvider;

            public Builder(
                ILogger logger,
                IRuntimeOperationIdProvider runtimeOperationIdProvider)
            {
                _logger = logger ?? throw new ArgumentNullException(nameof(logger));
                _runtimeOperationIdProvider = runtimeOperationIdProvider ?? throw new ArgumentNullException(nameof(runtimeOperationIdProvider));
                _variableHolder = new NumberVariableHolder(_logger, _runtimeOperationIdProvider);
                _variableHolder.Type = VariableType.Int; // default fallback
            }

            public Builder WithRawValue(int value)
            {
                _variableHolder.Value = value.ToString();
                _variableHolder.Type = VariableType.Int;
                return this;
            }

            public Builder WithRawValue(double value)
            {
                _variableHolder.Value = value.ToString();
                _variableHolder.Type = VariableType.Double;
                return this;
            }

            public Builder WithRawValue(float value)
            {
                _variableHolder.Value = value.ToString();
                _variableHolder.Type = VariableType.Float;
                return this;
            }

            public Builder WithRawValue(decimal value)
            {
                _variableHolder.Value = value.ToString();
                _variableHolder.Type = VariableType.Decimal;
                return this;
            }

            public Builder SetGlobal(bool isGlobal = true)
            {
                _variableHolder.IsGlobal = isGlobal;
                return this;
            }

            public async ValueTask<NumberVariableHolder> BuildAsync(CancellationToken token)
            {
                if (string.IsNullOrWhiteSpace(_variableHolder.Value))
                {
                    await _logger.LogAsync(_runtimeOperationIdProvider.OperationId, "The raw avalue of the holder can't be null or empty", LPSLoggingLevel.Error, token);
                    throw new InvalidOperationException("The raw avalue of the holder can't be null or empty");
                }

                return _variableHolder;
            }
        }
    }
}
