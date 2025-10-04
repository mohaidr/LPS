using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using LPS.Domain.Common.Interfaces;
using LPS.Domain.Domain.Common.Enums;
using LPS.Domain.Domain.Common.Interfaces;
using LPS.Infrastructure.Caching;
using LPS.Infrastructure.VariableServices.VariableHolders;

namespace LPS.Infrastructure.VariableServices
{
    public class VariableFactory : IVariableFactory
    {
        private readonly IPlaceholderResolverService _placeholderResolverService;
        ILogger _logger;
        IRuntimeOperationIdProvider _runtimeOperationIdProvider;

        public VariableFactory(IPlaceholderResolverService placeholderResolverService, 
            ILogger logger, 
            IRuntimeOperationIdProvider runtimeOperationIdProvider)
        {
            _placeholderResolverService = placeholderResolverService
                ?? throw new ArgumentNullException(nameof(placeholderResolverService));
            _logger = logger;
            _runtimeOperationIdProvider = runtimeOperationIdProvider;
        }

        public async Task<IStringVariableHolder> CreateStringAsync(
            string rawValue,
            VariableType type = VariableType.String,
            string? pattern = null,
            bool isGlobal = true,
            CancellationToken token = default)
        {
            // Only string-family types are allowed here
            if (type is not (VariableType.String or VariableType.QString or VariableType.QJsonString or VariableType.QCsvString or VariableType.QXmlString or VariableType.JsonString or VariableType.XmlString or VariableType.CsvString))
                throw new NotSupportedException($"Type '{type}' is not supported by CreateStringAsync.");

            var resolvedValue = await _placeholderResolverService.ResolvePlaceholdersAsync<string>(rawValue, string.Empty, token);
            var holder = await new StringVariableHolder.VBuilder(_placeholderResolverService, _logger, _runtimeOperationIdProvider)
                .WithType(type)
                .WithRawValue(resolvedValue)
                .WithPattern(pattern)
                .SetGlobal(isGlobal)
                .BuildAsync(token);

            return (IStringVariableHolder)holder;
        }

        public async Task<IVariableHolder> CreateBooleanAsync(
            string rawValue,
            bool isGlobal = true,
            CancellationToken token = default)
        {
            var resolvedValue = await _placeholderResolverService.ResolvePlaceholdersAsync<bool>(rawValue, string.Empty, token);
            var holder = await new BooleanVariableHolder.VBuilder(_logger, _runtimeOperationIdProvider)
                .WithRawValue(resolvedValue)
                .SetGlobal(isGlobal)
                .BuildAsync(token);

            return holder;
        }

        public async Task<IVariableHolder> CreateNumberAsync(
            string rawValue,
            VariableType type,
            bool isGlobal = true,
            CancellationToken token = default)
        {
            switch (type)
            {
                case VariableType.Int:
                    {
                        var resolvedValue = await _placeholderResolverService.ResolvePlaceholdersAsync<int>(rawValue, string.Empty, token);
                        var holder = await new NumberVariableHolder.VBuilder(_logger, _runtimeOperationIdProvider)
                            .WithRawValue(resolvedValue)
                            .SetGlobal(isGlobal)
                            .BuildAsync(token);
                        return holder;
                    }
                case VariableType.Float:
                    {
                        var resolvedValue = await _placeholderResolverService.ResolvePlaceholdersAsync<float>(rawValue, string.Empty, token);
                        var holder = await new NumberVariableHolder.VBuilder(_logger, _runtimeOperationIdProvider)
                            .WithRawValue(resolvedValue)
                            .SetGlobal(isGlobal)
                            .BuildAsync(token);
                        return holder;
                    }
                case VariableType.Double:
                    {
                        var resolvedValue = await _placeholderResolverService.ResolvePlaceholdersAsync<double>(rawValue, string.Empty, token);
                        var holder = await new NumberVariableHolder.VBuilder(_logger, _runtimeOperationIdProvider)
                            .WithRawValue(resolvedValue)
                            .SetGlobal(isGlobal)
                            .BuildAsync(token);
                        return holder;
                    }
                case VariableType.Decimal:
                    {
                        var resolvedValue = await _placeholderResolverService.ResolvePlaceholdersAsync<decimal>(rawValue, string.Empty, token);
                        var holder = await new NumberVariableHolder.VBuilder(_logger, _runtimeOperationIdProvider)
                            .WithRawValue(resolvedValue)
                            .SetGlobal(isGlobal)
                            .BuildAsync(token);
                        return holder;
                    }
                default:
                    throw new NotSupportedException($"Type '{type}' is not supported by CreateNumberAsync. Expected Int, Float, Double, Decimal.");
            }
        }

        public async Task<IHttpResponseVariableHolder> CreateHttpResponseAsync(
            IStringVariableHolder body,
            HttpStatusCode statusCode,
            IEnumerable<KeyValuePair<string, string>>? headers = null,
            bool isGlobal = false,
            CancellationToken token = default)
        {
            var builder = new HttpResponseVariableHolder.VBuilder(_placeholderResolverService, _logger, _runtimeOperationIdProvider)
                .WithBody(body)
                .WithStatusCode(statusCode)
                .SetGlobal(isGlobal);

            if (headers != null)
            {
                foreach (var kv in headers)
                    builder.WithHeader(kv.Key, kv.Value);
            }

            var holder = await builder.BuildAsync(token);
            return (IHttpResponseVariableHolder)holder;
        }


    }
}
