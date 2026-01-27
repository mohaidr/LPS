using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using LPS.Domain.Common;
using LPS.Domain.Common.Interfaces;
using LPS.Domain.Domain.Common.Enums;
using LPS.Domain.Domain.Common.Interfaces;
using LPS.Infrastructure.Common.Interfaces;
using LPS.Infrastructure.Logger;
using LPS.Infrastructure.VariableServices.GlobalVariableManager;

namespace LPS.Infrastructure.VariableServices.VariableHolders
{
    public sealed class KeyValueVariableHolder : IKeyValueVariableHolder
    {
        private readonly IPlaceholderResolverService _placeholderResolverService;
        private readonly ILogger _logger;
        private readonly IRuntimeOperationIdProvider _runtimeOperationIdProvider;

        private readonly VMaintainer _maintainer;
        public IVariableMaintainer Maintainer => _maintainer;

        public VariableType? Type { get; private set; } = VariableType.KeyValue;
        public bool IsGlobal { get; private set; }

        public KeyValuePair<string, IVariableHolder> KeyValue { get; private set; }

        private KeyValueVariableHolder(
            IPlaceholderResolverService resolver,
            ILogger logger,
            IRuntimeOperationIdProvider runtimeOperationIdProvider,
            bool isGlobal,
            VMaintainer maintainer)
        {
            _placeholderResolverService = resolver ?? throw new ArgumentNullException(nameof(resolver));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _runtimeOperationIdProvider = runtimeOperationIdProvider ?? throw new ArgumentNullException(nameof(runtimeOperationIdProvider));
            IsGlobal = isGlobal;
            _maintainer = maintainer ?? throw new ArgumentNullException(nameof(maintainer));

            KeyValue = new KeyValuePair<string, IVariableHolder>(string.Empty, default!);
        }

        public ValueTask<string> GetRawValueAsync(CancellationToken token)
        {
            // No intrinsic raw string for the key-value container itself
            return ValueTask.FromResult(string.Empty);
        }

        public async ValueTask<string> GetValueAsync(string? path, string sessionId, CancellationToken token)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                // If no path, just return raw value from the stored Value
                return await KeyValue.Value.GetRawValueAsync(token);
            }

            var resolvedPath = await _placeholderResolverService
                .ResolvePlaceholdersAsync<string>(path, sessionId, token);

            // Normalize: strip leading dots
            while (resolvedPath.StartsWith(".") && resolvedPath.Length > 1)
                resolvedPath = resolvedPath[1..];

            // Extract alias and rest (similar to MultipleVariableHolder)
            var dot = resolvedPath.IndexOf('.');
            var alias = dot >= 0 ? resolvedPath[..dot] : resolvedPath;
            var rest = dot >= 0 ? resolvedPath[dot..] : string.Empty;

            // If alias doesn't match our key, return empty (per your requirement)
            if (!string.Equals(alias, KeyValue.Key, StringComparison.OrdinalIgnoreCase))
                return string.Empty;

            // Delegate based on type
            if (KeyValue.Value is IObjectVariableHolder objHolder)
                return await objHolder.GetValueAsync(rest, sessionId, token);
            else
                return await KeyValue.Value.GetRawValueAsync(token);
        }

        public bool TryGetChild(string alias, out IVariableHolder child)
        {
            if (string.Equals(alias, KeyValue.Key, StringComparison.OrdinalIgnoreCase))
            {
                child = KeyValue.Value;
                return true;
            }
            child = null!;
            return false;
        }

        // -----------------------
        // Builder
        // -----------------------
        //This is a one one builder which will always return the same variable holder. If used in the intent of creating multiple different holders, this will result on logical errors
        public sealed class VMaintainer : IVariableMaintainer
        {
            private readonly IPlaceholderResolverService _placeholderResolverService;
            private readonly ILogger _logger;
            private readonly IRuntimeOperationIdProvider _runtimeOperationIdProvider;
            private readonly IVariableManager _manager;

            private readonly KeyValueVariableHolder _holder;

            private bool _isGlobal = true;
            private string _key = string.Empty;
            private IVariableHolder _value;

            public VMaintainer(
                IPlaceholderResolverService placeholderResolverService,
                ILogger logger,
                IRuntimeOperationIdProvider runtimeOperationIdProvider,
                IVariableManager manager)
            {
                _placeholderResolverService = placeholderResolverService ?? throw new ArgumentNullException(nameof(placeholderResolverService));
                _logger = logger ?? throw new ArgumentNullException(nameof(logger));
                _runtimeOperationIdProvider = runtimeOperationIdProvider ?? throw new ArgumentNullException(nameof(runtimeOperationIdProvider));
                _manager = manager ?? throw new ArgumentNullException(nameof(manager));

                _holder = new KeyValueVariableHolder(
                    _placeholderResolverService,
                    _logger,
                    _runtimeOperationIdProvider,
                    isGlobal: true,
                    maintainer: this)
                {
                    Type = VariableType.KeyValue
                };
            }

            public VMaintainer SetGlobal(bool isGlobal = true)
            {
                _isGlobal = isGlobal;
                return this;
            }

            public VMaintainer SetKey(string key)
            {
                if (string.IsNullOrWhiteSpace(key))
                    throw new ArgumentException("Key is required.", nameof(key));

                _key = key.Trim();
                return this;
            }

            public VMaintainer SetValue(IVariableHolder value)
            {
                _value = value ?? throw new ArgumentNullException(nameof(value));
                return this;
            }

            public async Task<VMaintainer> BindValueFromManagerAsync(string managerName, CancellationToken token)
            {
                if (string.IsNullOrWhiteSpace(managerName))
                    throw new ArgumentException("Manager variable name is required.", nameof(managerName));

                var val = await _manager.GetAsync(managerName.Trim(), token)
                          ?? throw new KeyNotFoundException($"Variable '{managerName}' not found in manager.");

                _value = val;
                return this;
            }

            public ValueTask<IVariableHolder> UpdateAsync(CancellationToken token)
            {
                token.ThrowIfCancellationRequested();

                if (string.IsNullOrWhiteSpace(_key))
                    throw new InvalidOperationException("Key must be set before building.");
                if (_value == null)
                    throw new InvalidOperationException("Value must be set before building.");

                _holder.IsGlobal = _isGlobal;
                _holder.KeyValue = new KeyValuePair<string, IVariableHolder>(_key, _value);

                return ValueTask.FromResult((IVariableHolder)_holder);
            }
        }
    }
}
