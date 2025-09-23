using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LPS.Domain.Common;
using LPS.Domain.Common.Interfaces;
using LPS.Domain.Domain.Common.Enums;
using LPS.Infrastructure.Common.Interfaces;
using LPS.Infrastructure.Logger;
using LPS.Infrastructure.VariableServices.GlobalVariableManager;

namespace LPS.Infrastructure.VariableServices.VariableHolders
{
    public sealed class MultipleVariableHolder : IObjectVariableHolder
    {
        private readonly IPlaceholderResolverService _placeholderResolverService;
        private readonly ILogger _logger;
        private readonly IRuntimeOperationIdProvider _runtimeOperationIdProvider;

        // Private children: alias -> IVariableHolder (not registered in the manager by default)
        private readonly Dictionary<string, IVariableHolder> _children =
            new(StringComparer.OrdinalIgnoreCase);
        private readonly VBuilder _builder;
        public IVariableBuilder Builder => _builder;

        private MultipleVariableHolder(
            IPlaceholderResolverService resolver,
            ILogger logger,
            IRuntimeOperationIdProvider runtimeOperationIdProvider,
            bool isGlobal, VBuilder builder)
        {
            _placeholderResolverService = resolver;
            _logger = logger;
            _runtimeOperationIdProvider = runtimeOperationIdProvider;
            IsGlobal = isGlobal;
            _builder = builder;
        }

        // IVariableHolder members
        public VariableType? Type { get; private set; } = VariableType.Multiple;
        public bool IsGlobal { get; private set; }


        public ValueTask<string> GetRawValueAsync(CancellationToken token)
        {
            // Keep it simple for object: no intrinsic raw string
            return ValueTask.FromResult(string.Empty);
        }

        public async ValueTask<string> GetValueAsync(string? path, string sessionId, CancellationToken token)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                await _logger.LogAsync(_runtimeOperationIdProvider.OperationId,
                    "ObjectVariableHolder requires a child path like '.response' or '.throughput'.",
                    LPSLoggingLevel.Error, token);
                throw new NotSupportedException("ObjectVariableHolder requires a child path like '.response'.");
            }

            var resolvedPath = await _placeholderResolverService
                .ResolvePlaceholdersAsync<string>(path, sessionId, token);

            // Normalize: strip leading dots
            while (resolvedPath.StartsWith(".") && resolvedPath.Length > 1) resolvedPath = resolvedPath[1..];

            // Extract first segment (alias) and remainder (keep leading dot for child)
            var dot = resolvedPath.IndexOf('.');
            var alias = dot >= 0 ? resolvedPath[..dot] : resolvedPath;
            var rest = dot >= 0 ? resolvedPath[dot..] : string.Empty; // may be empty or like ".Body..."

            if (string.IsNullOrWhiteSpace(alias))
            {
                await _logger.LogAsync(_runtimeOperationIdProvider.OperationId,
                    $"Invalid path '{path}'. Expected a child alias after '.'.",
                    LPSLoggingLevel.Error, token);
                throw new ArgumentException($"Invalid path '{path}'.");
            }

            if (!_children.TryGetValue(alias, out var child))
            {
                await _logger.LogAsync(_runtimeOperationIdProvider.OperationId,
                    $"Child '{alias}' not found in ObjectVariableHolder.",
                    LPSLoggingLevel.Error, token);
                throw new KeyNotFoundException($"Child '{alias}' not found.");
            }

            if (child is IObjectVariableHolder)
                // Delegate to child with remaining path
                return await ((IObjectVariableHolder)child).GetValueAsync(rest, sessionId, token);
            else
                return await child.GetRawValueAsync(token);

        }

        public bool TryGetChild(string alias, out IVariableHolder child)
          => _children.TryGetValue(alias ?? string.Empty, out child);

        // -----------------------
        // Builder
        // -----------------------
        //This is a one one builder which will always return the same variable holder. If used in the intent of creating multiple different holders, this will result on logical errors
        public sealed class VBuilder : IVariableBuilder
        {
            // Services
            private readonly IPlaceholderResolverService _placeholderResolverService;
            private readonly ILogger _logger;
            private readonly IRuntimeOperationIdProvider _runtimeOperationIdProvider;
            private readonly IVariableManager _manager;

            // Pre-created holder (keep untouched until BuildAsync)
            private readonly MultipleVariableHolder _holder;

            // Local buffered state
            private bool _isGlobal = true;
            private readonly Dictionary<string, IVariableHolder> _children =
                new(StringComparer.OrdinalIgnoreCase);

            public VBuilder(
                IPlaceholderResolverService placeholderResolverService,
                ILogger logger,
                IRuntimeOperationIdProvider runtimeOperationIdProvider,
                IVariableManager manager)
            {
                _placeholderResolverService = placeholderResolverService ?? throw new ArgumentNullException(nameof(placeholderResolverService));
                _logger = logger ?? throw new ArgumentNullException(nameof(logger));
                _runtimeOperationIdProvider = runtimeOperationIdProvider ?? throw new ArgumentNullException(nameof(runtimeOperationIdProvider));
                _manager = manager ?? throw new ArgumentNullException(nameof(manager));

                // Create holder instance once
                _holder = new MultipleVariableHolder(
                    _placeholderResolverService,
                    _logger,
                    _runtimeOperationIdProvider,
                    isGlobal: true,
                    builder: this)
                {
                    Type = VariableType.Multiple
                };
            }

            public VBuilder SetGlobal(bool isGlobal = true)
            {
                _isGlobal = isGlobal; // buffer locally
                return this;
            }

            /// <summary>
            /// Attach a private child (local to this object). Does not register in manager.
            /// </summary>
            public VBuilder AttachChild(string alias, IVariableHolder child)
            {
                if (string.IsNullOrWhiteSpace(alias))
                    throw new ArgumentException("Alias is required.", nameof(alias));

                _children[alias.Trim()] = child ?? throw new ArgumentNullException(nameof(child));
                return this;
            }

            /// <summary>
            /// Bind a reference to an existing variable stored in the manager (no copy).
            /// </summary>
            public async Task<VBuilder> BindChildFromManagerAsync(string alias, string managerName, CancellationToken token)
            {
                if (string.IsNullOrWhiteSpace(alias))
                    throw new ArgumentException("Alias is required.", nameof(alias));
                if (string.IsNullOrWhiteSpace(managerName))
                    throw new ArgumentException("Manager variable name is required.", nameof(managerName));

                var child = await _manager.GetAsync(managerName.Trim(), token)
                            ?? throw new KeyNotFoundException($"Variable '{managerName}' not found in manager.");

                _children[alias.Trim()] = child;
                return this;
            }

            public ValueTask<IVariableHolder> BuildAsync(CancellationToken token)
            {
                token.ThrowIfCancellationRequested();

                // Assign buffered values to the pre-created holder
                _holder.IsGlobal = _isGlobal;

                foreach (var kv in _children)
                {
                    _holder._children[kv.Key] = kv.Value;
                }

                return ValueTask.FromResult((IVariableHolder)_holder);
            }
        }
    }
}
