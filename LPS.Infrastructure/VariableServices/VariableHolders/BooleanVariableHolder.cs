using LPS.Domain.Common;
using LPS.Domain.Common.Interfaces;
using LPS.Domain.Domain.Common.Enums;
using LPS.Infrastructure.Common.Interfaces;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace LPS.Infrastructure.VariableServices.VariableHolders
{
    public class BooleanVariableHolder : IVariableHolder
    {
        public string Value { get; private set; }

        public VariableType? Type { get; private set; }
        public bool IsGlobal { get; private set; }

        private readonly ILogger _logger;
        private readonly IRuntimeOperationIdProvider _runtimeOperationIdProvider;
        private readonly VBuilder _builder;
        public IVariableBuilder Builder => _builder;

        private BooleanVariableHolder(
            ILogger logger,
            IRuntimeOperationIdProvider runtimeOperationIdProvider, VBuilder builder)
        {
            
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _runtimeOperationIdProvider = runtimeOperationIdProvider ?? throw new ArgumentNullException(nameof(runtimeOperationIdProvider));
            _builder = builder ?? throw new ArgumentNullException(nameof(builder));
        }

        public ValueTask<string> GetRawValueAsync(CancellationToken token) => ValueTask.FromResult(Value);

        // ======= Builder Class =======
        //This is a one one builder which will always return the same variable holder. If used in the intent of creating multiple different holders, this will result on logical errors
        public sealed class VBuilder : IVariableBuilder
        {
            private readonly ILogger _logger;
            private readonly IRuntimeOperationIdProvider _runtimeOperationIdProvider;

            // Pre-created holder: do NOT touch it until BuildAsync
            private readonly BooleanVariableHolder _holder;

            // Local buffered state
            private bool? _rawValue;
            private bool _isGlobal;

            public VBuilder(
                ILogger logger,
                IRuntimeOperationIdProvider runtimeOperationIdProvider)
            {
                _logger = logger ?? throw new ArgumentNullException(nameof(logger));
                _runtimeOperationIdProvider = runtimeOperationIdProvider ?? throw new ArgumentNullException(nameof(runtimeOperationIdProvider));

                // Create the holder once here; assignments happen later in BuildAsync
                _holder = new BooleanVariableHolder(_logger, _runtimeOperationIdProvider, this)
                {
                    Type = VariableType.Boolean
                };
            }

            public VBuilder WithRawValue(bool value)
            {
                _rawValue = value; // buffer locally
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

                // Validate local buffered state before assigning to the holder
                if (!_rawValue.HasValue)
                {
                    await _logger.LogAsync(
                        _runtimeOperationIdProvider.OperationId,
                        "The raw value of the BooleanVariableHolder must be provided.",
                        LPSLoggingLevel.Error,
                        token);

                    throw new InvalidOperationException("The raw value of the BooleanVariableHolder must be provided.");
                }

                // Assign buffered values atomically to the pre-created holder
                _holder.Value = _rawValue.Value.ToString();
                _holder.IsGlobal = _isGlobal;

                return _holder; // return the same instance created in the constructor
            }

        }
    }
}
