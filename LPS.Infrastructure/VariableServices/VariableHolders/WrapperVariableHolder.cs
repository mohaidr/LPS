#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;
using LPS.Domain.Common;
using LPS.Domain.Common.Interfaces;
using LPS.Domain.Domain.Common.Enums;
using LPS.Infrastructure.Common.Interfaces;

namespace LPS.Infrastructure.VariableServices.VariableHolders
{
    /// <summary>
    /// Wraps any IVariableHolder and exposes object-style navigation when the inner holder supports it.
    /// </summary>
    public sealed class WrapperVariableHolder : IWrapperVariableHolder
    {
        private readonly IPlaceholderResolverService _placeholderResolverService;
        private readonly ILogger _logger;
        private readonly IRuntimeOperationIdProvider _runtimeOperationIdProvider;
        private readonly VBuilder _builder;

        private IVariableHolder _inner = default!;

        private WrapperVariableHolder(
            IPlaceholderResolverService placeholderResolverService,
            ILogger logger,
            IRuntimeOperationIdProvider runtimeOperationIdProvider,
            VBuilder builder)
        {
            _placeholderResolverService = placeholderResolverService ?? throw new ArgumentNullException(nameof(placeholderResolverService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _runtimeOperationIdProvider = runtimeOperationIdProvider ?? throw new ArgumentNullException(nameof(runtimeOperationIdProvider));
            _builder = builder ?? throw new ArgumentNullException(nameof(builder));

            Type = VariableType.Object; // wrapper is an object-style holder
        }

        // IVariableHolder
        public VariableType? Type { get; private set; }
        public bool IsGlobal { get; private set; }
        public IVariableBuilder Builder => _builder;

        // IWrapperVariableHolder
        public IVariableHolder VariableHolder => _inner;

        public ValueTask<string> GetRawValueAsync()
            => _inner != null
                ? _inner.GetRawValueAsync()
                : ValueTask.FromResult(string.Empty);

        /// <summary>
        /// Delegates to the wrapped holder if it implements IObjectVariableHolder.
        /// If no path is provided, returns the raw value of the wrapped holder.
        /// </summary>
        public async ValueTask<string> GetValueAsync(string? path, string sessionId, CancellationToken token)
        {
            if (_inner == null)
            {
                await _logger.LogAsync(
                    _runtimeOperationIdProvider.OperationId,
                    "WrapperVariableHolder has no inner VariableHolder.",
                    LPSLoggingLevel.Error, token);
                throw new InvalidOperationException("WrapperVariableHolder is not initialized with an inner VariableHolder.");
            }

            // No path? return inner raw value
            if (string.IsNullOrWhiteSpace(path))
                return await _inner.GetRawValueAsync();

            var resolvedPath = await _placeholderResolverService
                .ResolvePlaceholdersAsync<string>(path, sessionId, token);

            if (_inner is IObjectVariableHolder objectHolder)
            {
                // Delegate navigation to the inner object holder
                return await objectHolder.GetValueAsync(resolvedPath, sessionId, token);
            }

            // Non-object inner cannot be navigated with a path
            await _logger.LogAsync(
                _runtimeOperationIdProvider.OperationId,
                $"Inner VariableHolder of type '{_inner.GetType().Name}' does not support path navigation '{resolvedPath}'.",
                LPSLoggingLevel.Error, token);

            throw new NotSupportedException("Inner VariableHolder does not support object path navigation.");
        }

        // -----------------------
        // Builder
        // -----------------------
        public sealed class VBuilder : IVariableBuilder
        {
            private readonly IPlaceholderResolverService _placeholderResolverService;
            private readonly ILogger _logger;
            private readonly IRuntimeOperationIdProvider _runtimeOperationIdProvider;

            private readonly WrapperVariableHolder _holder;

            // Buffered state
            private IVariableHolder? _inner;
            private bool _isGlobal;

            public VBuilder(
                IPlaceholderResolverService placeholderResolverService,
                ILogger logger,
                IRuntimeOperationIdProvider runtimeOperationIdProvider)
            {
                _placeholderResolverService = placeholderResolverService ?? throw new ArgumentNullException(nameof(placeholderResolverService));
                _logger = logger ?? throw new ArgumentNullException(nameof(logger));
                _runtimeOperationIdProvider = runtimeOperationIdProvider ?? throw new ArgumentNullException(nameof(runtimeOperationIdProvider));

                _holder = new WrapperVariableHolder(
                    _placeholderResolverService,
                    _logger,
                    _runtimeOperationIdProvider,
                    this);
            }

            /// <summary>
            /// Sets the inner variable holder to be wrapped (required).
            /// </summary>
            public VBuilder WithVariable(IVariableHolder inner)
            {
                _inner = inner ?? throw new ArgumentNullException(nameof(inner));
                return this;
            }

            public VBuilder SetGlobal(bool isGlobal = true)
            {
                _isGlobal = isGlobal;
                return this;
            }

            public ValueTask<IVariableHolder> BuildAsync(CancellationToken token)
            {
                token.ThrowIfCancellationRequested();

                if (_inner is null)
                {
                    throw new InvalidOperationException("WrapperVariableHolder requires an inner VariableHolder. Call WithVariable(...) first.");
                }

                _holder._inner = _inner;
                _holder.IsGlobal = _isGlobal;

                // Keep Type as Object to indicate object-style holder regardless of inner type.
                _holder.Type = VariableType.Object;

                return ValueTask.FromResult<IVariableHolder>(_holder);
            }
        }
    }
}
