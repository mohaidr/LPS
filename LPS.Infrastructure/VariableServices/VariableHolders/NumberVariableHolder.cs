﻿using LPS.Domain.Common;
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
        private readonly ILogger _logger;
        private readonly IRuntimeOperationIdProvider _runtimeOperationIdProvider;
        private readonly VBuilder _builder;
        public IVariableBuilder Builder => _builder;

        private NumberVariableHolder(
            ILogger logger,
            IRuntimeOperationIdProvider runtimeOperationIdProvider, VBuilder builder)
        {

            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _runtimeOperationIdProvider = runtimeOperationIdProvider ?? throw new ArgumentNullException(nameof(runtimeOperationIdProvider));
            _builder = builder ?? throw new ArgumentNullException(nameof(builder));
        }

        public ValueTask<string> GetRawValueAsync(CancellationToken token) => ValueTask.FromResult(Value);

        // ====== Builder ======
        //This is a one one builder which will always return the same variable holder. If used in the intent of creating multiple different holders, this will result on logical errors
        public sealed class VBuilder : IVariableBuilder
        {
            private readonly NumberVariableHolder _variableHolder;
            private readonly ILogger _logger;
            private readonly IRuntimeOperationIdProvider _runtimeOperationIdProvider;

            // Local buffered state (do NOT touch _variableHolder until BuildAsync)
            private string _rawValue;
            private VariableType _type = VariableType.Int; // default fallback
            private bool _isGlobal;

            public VBuilder(
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
            public VBuilder WithRawValue(int value)
            {
                _rawValue = value.ToString(System.Globalization.CultureInfo.InvariantCulture);
                _type = VariableType.Int;
                return this;
            }

            public VBuilder WithRawValue(double value)
            {
                _rawValue = value.ToString(System.Globalization.CultureInfo.InvariantCulture);
                _type = VariableType.Double;
                return this;
            }

            public VBuilder WithRawValue(float value)
            {
                _rawValue = value.ToString(System.Globalization.CultureInfo.InvariantCulture);
                _type = VariableType.Float;
                return this;
            }

            public VBuilder WithRawValue(decimal value)
            {
                _rawValue = value.ToString(System.Globalization.CultureInfo.InvariantCulture);
                _type = VariableType.Decimal;
                return this;
            }

            public VBuilder SetGlobal(bool isGlobal = true)
            {
                _isGlobal = isGlobal; // buffer locally
                return this;
            }

            public async ValueTask<IVariableHolder> BuildAsync(CancellationToken token)
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
                _variableHolder.Type = _type;
                _variableHolder.IsGlobal = _isGlobal;

                return _variableHolder;
            }
        }
    }
}
