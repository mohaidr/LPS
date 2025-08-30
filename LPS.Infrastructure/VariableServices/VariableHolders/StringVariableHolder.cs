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
using LPS.Infrastructure.Common.Interfaces;

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
        private readonly VBuilder _builder;
        public IVariableBuilder Builder => _builder;

        private StringVariableHolder(IPlaceholderResolverService resolver, ILogger logger, IRuntimeOperationIdProvider runtimeOperationIdProvider, VBuilder builder)
        {
            _placeholderResolverService = resolver ?? throw new ArgumentNullException(nameof(resolver));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _runtimeOperationIdProvider = runtimeOperationIdProvider ?? throw new ArgumentNullException(nameof(runtimeOperationIdProvider));
            _builder = builder ?? throw new ArgumentNullException(nameof(builder));
        }

        public ValueTask<string> GetRawValueAsync(CancellationToken token)
        {
            return (Type == VariableType.QString || Type == VariableType.QCsvString || Type == VariableType.QJsonString || Type == VariableType.QXmlString)  ? ValueTask.FromResult($"\"{Value}\"") : ValueTask.FromResult(Value);
        }

        public async ValueTask<string> GetValueAsync(string? path, string sessionId, CancellationToken token)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return await GetRawValueAsync(token); 
            }
            else if (Type == VariableType.String || Type == VariableType.QString)
            {
                await _logger.LogAsync(_runtimeOperationIdProvider.OperationId, $"A variable of type string can't have path. Empty value will be returned", LPSLoggingLevel.Warning, token);
                return Type== VariableType.QString ? "\"\"": string.Empty;
            }    

            var resolvedPath = await _placeholderResolverService.ResolvePlaceholdersAsync<string>(path ?? string.Empty, sessionId, token);
            string extracted = Type switch
            {
                VariableType.JsonString => await ExtractJsonValue(resolvedPath ?? "", sessionId, token),
                VariableType.XmlString => await ExtractXmlValue(resolvedPath ?? "", sessionId, token),
                VariableType.CsvString => await ExtractCsvValueAsync(resolvedPath ?? "", sessionId, token),
                VariableType.QJsonString => $"\"{await ExtractJsonValue(resolvedPath ?? "", sessionId, token)}\"",
                VariableType.QXmlString => $"\"{await ExtractXmlValue(resolvedPath ?? "", sessionId, token)}\"",
                VariableType.QCsvString => $"\"{await ExtractCsvValueAsync(resolvedPath ?? "", sessionId, token)}\"",
                VariableType.QString => $"\"{Value}\"",
                    VariableType.String => Value,
                _ => throw new NotSupportedException($"Format '{Type}' is not supported by StringVariableHolder.")
            };

            var result = await ApplyRegexIfNeededAsync(extracted, token);

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
                    await _logger.LogAsync(_runtimeOperationIdProvider.OperationId, $"JSON path '{jsonPath}' not found. Empty value will be returned", LPSLoggingLevel.Error, token);
                    return string.Empty;
                }

                return tokenResult;
            }
            catch (Exception ex)
            {
                await _logger.LogAsync(_runtimeOperationIdProvider.OperationId, $"Failed to extract JSON value using path '{jsonPath}'.\n{ex}", LPSLoggingLevel.Error, token);
                return string.Empty;
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
                    await _logger.LogAsync(_runtimeOperationIdProvider.OperationId, $"XPath '{xpath}' not found. Empty value will be returned", LPSLoggingLevel.Error, token);
                    return string.Empty;
                }
                return node?.InnerText;

            }
            catch (Exception ex)
            {
                await _logger.LogAsync(_runtimeOperationIdProvider.OperationId, $"Failed to extract XML value using XPath '{xpath}'.\n{ex}", LPSLoggingLevel.Error, token);
                return string.Empty;
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
                {
                    await _logger.LogAsync(_runtimeOperationIdProvider.OperationId, $"Row index {rowIndex} is out of range. Returning empty value", LPSLoggingLevel.Error, token);
                    return string.Empty;
                }
                var record = (IDictionary<string, object>)records[rowIndex];

                if (columnIndex < 0 || columnIndex >= record.Count)
                {
                    await _logger.LogAsync(_runtimeOperationIdProvider.OperationId, $"Column index {columnIndex} is out of range. Returning empty value", LPSLoggingLevel.Error, token);
                    return string.Empty;
                }
                return record.Values.ElementAt(columnIndex)?.ToString() ?? string.Empty;
            }
            catch (Exception ex)
            {
                await _logger.LogAsync(_runtimeOperationIdProvider.OperationId, $"Failed to extract csv value using indices '{indices}'.\n{ex}", LPSLoggingLevel.Error, token);
                return string.Empty;
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
        public sealed class VBuilder : IVariableBuilder
        {
            private readonly StringVariableHolder _variableHolder;
            private readonly IPlaceholderResolverService _placeholderResolverService;
            private readonly ILogger _logger;
            private readonly IRuntimeOperationIdProvider _runtimeOperationIdProvider;

            // Local buffered state (do NOT touch _variableHolder until BuildAsync)
            private VariableType? _type;          // defaults to holder's initial type if not set
            private string _pattern;              // may be null/empty
            private string _rawValue;             // must be provided
            private bool _isGlobal;               // default false unless set

            public VBuilder(
                IPlaceholderResolverService placeholderResolverService,
                ILogger logger,
                IRuntimeOperationIdProvider runtimeOperationIdProvider)
            {
                _placeholderResolverService = placeholderResolverService ?? throw new ArgumentNullException(nameof(placeholderResolverService));
                _logger = logger ?? throw new ArgumentNullException(nameof(logger));
                _runtimeOperationIdProvider = runtimeOperationIdProvider ?? throw new ArgumentNullException(nameof(runtimeOperationIdProvider));

                // Pre-create holder; keep it untouched until BuildAsync
                _variableHolder = new StringVariableHolder(_placeholderResolverService, _logger, _runtimeOperationIdProvider, this)
                {
                    Type = VariableType.String // default
                };
            }

            public VBuilder WithType(VariableType type)
            {
                _type = type; // buffer locally
                return this;
            }

            public VBuilder WithPattern(string pattern)
            {
                _pattern = pattern; // buffer locally
                return this;
            }

            public VBuilder WithRawValue(string value)
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

                if (string.IsNullOrWhiteSpace(_rawValue))
                {
                    await _logger.LogAsync(
                        _runtimeOperationIdProvider.OperationId,
                        "The raw value of the StringVariableHolder can't be null or empty.",
                        LPSLoggingLevel.Error,
                        token);

                    throw new InvalidOperationException("The raw value of the StringVariableHolder can't be null or empty.");
                }

                // Assign buffered values atomically to the pre-created holder
                if (_type.HasValue) _variableHolder.Type = _type.Value;
                _variableHolder.Pattern = _pattern ?? string.Empty;
                _variableHolder.Value = _rawValue;
                _variableHolder.IsGlobal = _isGlobal;

                return _variableHolder;
            }
        }
    }

}
