using CsvHelper.Configuration;
using CsvHelper;
using LPS.Domain.Common.Interfaces;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using LPS.Domain.Common;
using LPS.Domain.Domain.Common.Enums;
using LPS.Infrastructure.Caching;
using LPS.Infrastructure.LPSClients.CachService;

namespace LPS.Infrastructure.VariableServices.VariableHolders
{
    public class StringVariableHolder : IStringVariableHolder
    {
        private readonly IPlaceholderResolverService _placeholderResolverService;

        public VariableType? Type { get; private set; }
        public string Pattern { get; private set; }
        public bool IsGlobal { get; private set; }
        public string Value { get; private set; }
        ILogger _logger;
        IRuntimeOperationIdProvider _runtimeOperationIdProvider;
        ICacheService<string> _memoryCacheService;
        private StringVariableHolder(IPlaceholderResolverService resolver, ILogger logger, IRuntimeOperationIdProvider runtimeOperationIdProvider, ICacheService<string> memoryCacheService)
        {
            _placeholderResolverService = resolver ?? throw new ArgumentNullException(nameof(resolver));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _runtimeOperationIdProvider = runtimeOperationIdProvider ?? throw new ArgumentNullException(nameof(runtimeOperationIdProvider));
            _memoryCacheService = memoryCacheService ?? throw new ArgumentNullException(nameof(memoryCacheService));
        }

        public ValueTask<string> GetRawValueAsync()
        {
            return ValueTask.FromResult(Value);
        }

        public async ValueTask<string> GetValueAsync(string? path, string sessionId, CancellationToken token)
        {
            if (string.IsNullOrWhiteSpace(path))
                return await GetRawValueAsync();
            var hash = this.GetHashCode();
            var resolvedPath = await _placeholderResolverService.ResolvePlaceholdersAsync<string>(path ?? string.Empty, sessionId, token);
            var cacheKey = $"{CachePrefixes.Variable}{hash}_{resolvedPath}";
            if (_memoryCacheService.TryGetItem(cacheKey, out string cachedContent))
            {
                return cachedContent;
            }
            string extracted = Type switch
            {
                VariableType.JsonString => await ExtractJsonValue(resolvedPath ?? "", sessionId, token),
                VariableType.XmlString => await ExtractXmlValue(resolvedPath ?? "", sessionId, token),
                VariableType.CsvString => await ExtractCsvValueAsync(resolvedPath ?? "", sessionId, token),
                VariableType.String => Value,
                _ => throw new NotSupportedException($"Format '{Type}' is not supported by StringVariableHolder.")
            };

            var result = await ApplyRegexIfNeededAsync(extracted, token);

            // Cache only if the path is null OR (the path has no embedded placeholder)
            // Example where we skip caching: ${csvData[$loopcounter(...),0]} because it changes every request
            if (path == null || !path.Contains('$'))
                await _memoryCacheService.SetItemAsync(cacheKey, result, TimeSpan.FromSeconds(30));
            return result;
        }

        private async ValueTask<string> ExtractJsonValue(string jsonPath, string sessionId, CancellationToken token)
        {
            try
            {
                var json = JToken.Parse(Value);
                var tokenResult = json.SelectToken(jsonPath)?.ToString();

                if (tokenResult == null)
                {
                    throw new InvalidOperationException($"JSON path '{jsonPath}' not found.");
                }

                return tokenResult;
            }
            catch (Exception ex)
            {
                await _logger.LogAsync(_runtimeOperationIdProvider.OperationId, $"{ex}", LPSLoggingLevel.Error, token);
                throw new InvalidOperationException($"Failed to extract JSON value using path '{jsonPath}'.", ex);
            }
        }

        private async ValueTask<string> ExtractXmlValue(string xpath, string sessionId, CancellationToken token)
        {
            try
            {
                var doc = new XmlDocument();
                doc.LoadXml(Value);
                var node = doc.SelectSingleNode(xpath);

                if (node?.InnerText == null)
                {
                    throw new InvalidOperationException($"XPath '{xpath}' not found.");
                }
                return node?.InnerText;

            }
            catch (Exception ex)
            {
                await _logger.LogAsync(_runtimeOperationIdProvider.OperationId, $"{ex}", LPSLoggingLevel.Error, token);

                throw new InvalidOperationException($"Failed to extract XML value using XPath '{xpath}'.", ex);
            }
        }

        private async ValueTask<string> ExtractCsvValueAsync(string indices, string sessionId, CancellationToken token)
        {
            try
            {
                var trimmed = indices.Trim('[', ']');
                var parts = trimmed.Split(',');
                if (parts.Length != 2 ||
                    !int.TryParse(parts[0], out int rowIndex) ||
                    !int.TryParse(parts[1], out int columnIndex))
                {
                    throw new ArgumentException("Invalid index format. Use the format [rowIndex,columnIndex].");
                }

                var config = new CsvConfiguration(CultureInfo.InvariantCulture)
                {
                    HasHeaderRecord = true
                };

                using var reader = new StringReader(Value);
                using var csv = new CsvReader(reader, config);
                var records = csv.GetRecords<dynamic>().ToList();

                if (rowIndex < 0 || rowIndex >= records.Count)
                    throw new IndexOutOfRangeException("Row index is out of range.");

                var record = (IDictionary<string, object>)records[rowIndex];

                if (columnIndex < 0 || columnIndex >= record.Count)
                    throw new IndexOutOfRangeException("Column index is out of range.");

                return record.Values.ElementAt(columnIndex)?.ToString() ?? string.Empty;
            }
            catch (Exception ex)
            {
                await _logger.LogAsync(_runtimeOperationIdProvider.OperationId, $"{ex}", LPSLoggingLevel.Error, token);
                throw;
            }
        }

        private async ValueTask<string> ApplyRegexIfNeededAsync(string value, CancellationToken token)
        {
            if (string.IsNullOrWhiteSpace(Pattern)) return value;

            try
            {
                var match = Regex.Match(value, Pattern);
                return match.Success ? match.Value : throw new InvalidOperationException($"Pattern '{Pattern}' did not match.");
            }
            catch (Exception ex)
            {
                await _logger.LogAsync(_runtimeOperationIdProvider.OperationId, $"{ex}", LPSLoggingLevel.Error, token);
                throw new InvalidOperationException($"Failed to apply regex pattern '{Pattern}'.", ex);
            }
        }
        public class Builder
        {
            private readonly StringVariableHolder _variableHolder;
            private readonly IPlaceholderResolverService _placeholderResolverService;
            private readonly ILogger _logger;
            private readonly IRuntimeOperationIdProvider _runtimeOperationIdProvider;
            ICacheService<string> _memoryCacheService;



            public Builder(
                IPlaceholderResolverService placeholderResolverService,
                ILogger logger,
                IRuntimeOperationIdProvider runtimeOperationIdProvider,
                ICacheService<string> memoryCacheService)
            {
                _placeholderResolverService = placeholderResolverService ?? throw new ArgumentNullException(nameof(placeholderResolverService));
                _logger = logger ?? throw new ArgumentNullException(nameof(logger));
                _runtimeOperationIdProvider = runtimeOperationIdProvider ?? throw new ArgumentNullException(nameof(runtimeOperationIdProvider));
                _memoryCacheService = memoryCacheService ?? throw new ArgumentNullException();
                _variableHolder = new StringVariableHolder(_placeholderResolverService, _logger, _runtimeOperationIdProvider, _memoryCacheService)
                {
                    Type = VariableType.String // default
                };
            }


            public Builder WithType(VariableType type)
            {
                _variableHolder.Type = type;
                return this;
            }

            public Builder WithPattern(string pattern)
            {
                _variableHolder.Pattern = pattern;
                return this;
            }

            public Builder WithRawValue(string value)
            {
                _variableHolder.Value = value;
                return this;
            }

            public Builder SetGlobal(bool isGlobal = true)
            {
                _variableHolder.IsGlobal = isGlobal;
                return this;
            }

            public async ValueTask<StringVariableHolder> BuildAsync(CancellationToken token)
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
