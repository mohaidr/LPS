using LPS.Domain.Common;
using System;

namespace LPS.Infrastructure.LPSClients.SessionManager
{
    public class VariableHolder : IVariableHolder
    {
        public MimeType Format { get; private set; }
        public string Pattern { get; private set; }
        public string RawValue { get; private set; }
        public bool IsGlobal { get; private set; }

        private VariableHolder() { } // Private constructor for controlled instantiation via builder

        public bool CheckIfSupportsParsing(MimeType mimeType)
        {
            return mimeType == MimeType.ApplicationJson ||
                   mimeType == MimeType.RawXml ||
                   mimeType == MimeType.TextXml ||
                   mimeType == MimeType.ApplicationXml ||
                   mimeType == MimeType.TextPlain;
        }

        public string ExtractJsonValue(string jsonPath)
        {
            if (Format != MimeType.ApplicationJson)
                throw new InvalidOperationException("Response is not JSON.");

            try
            {
                var json = Newtonsoft.Json.Linq.JObject.Parse(RawValue);
                var token = json.SelectToken(jsonPath)?.ToString() ?? throw new InvalidOperationException($"JSON path '{jsonPath}' not found.");
                return ExtractRegexMatch(token, Pattern);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to extract JSON value using path '{jsonPath}'.", ex);
            }
        }

        public string ExtractXmlValue(string xpath)
        {
            if (Format != MimeType.TextXml && Format != MimeType.ApplicationXml && Format != MimeType.RawXml)
            {
                throw new InvalidOperationException("Response is not XML.");
            }

            try
            {
                var doc = new System.Xml.XmlDocument();
                doc.LoadXml(RawValue);
                var node = doc.SelectSingleNode(xpath);
                return ExtractRegexMatch(node?.InnerText, Pattern) ?? throw new InvalidOperationException($"XPath '{xpath}' not found.");
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to extract XML value using XPath '{xpath}'.", ex);
            }
        }

        public string ExtractValueWithRegex()
        {
            return ExtractRegexMatch(RawValue, Pattern);
        }

        private static string ExtractRegexMatch(string value, string pattern)
        {
            if (string.IsNullOrEmpty(pattern))
                return value;

            try
            {
                var match = System.Text.RegularExpressions.Regex.Match(value, pattern);
                return match.Success ? match.Value : throw new InvalidOperationException($"Pattern '{pattern}' did not match.");
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to apply regex pattern '{pattern}'.", ex);
            }
        }

        // Builder class
        public class Builder
        {
            private readonly VariableHolder _variableHolder;

            public Builder()
            {
                _variableHolder = new VariableHolder();
            }

            public Builder WithFormat(MimeType format)
            {
                _variableHolder.Format = format;
                return this;
            }

            public Builder WithPattern(string pattern)
            {
                _variableHolder.Pattern = pattern;
                return this;
            }

            public Builder WithRawResponse(string rawResponse)
            {
                _variableHolder.RawValue = rawResponse;
                return this;
            }


            public Builder SetGlobal(bool isGlobal)
            {
                _variableHolder.IsGlobal = isGlobal;
                return this;
            }


            public VariableHolder Build()
            {
                return _variableHolder;
            }
        }
    }
}
