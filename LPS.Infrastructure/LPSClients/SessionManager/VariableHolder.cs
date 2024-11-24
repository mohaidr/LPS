using LPS.Domain.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LPS.Infrastructure.LPSClients.SessionManager
{
    public class VariableHolder(string rawResponse, MimeType format, string pattern) : IVariableHolder
    {
        public MimeType Format { get; private set; } = format != MimeType.Unknown ? format : throw new ArgumentNullException(nameof(format));
        public string Pattern { get; private set; }   = pattern ?? throw new ArgumentNullException(nameof(pattern));
        public string RawResponse { get; private set; } = rawResponse ?? throw new ArgumentNullException(nameof(rawResponse));

        public string ExtractJsonValue(string jsonPath)
        {
            if (Format != MimeType.ApplicationJson)
                throw new InvalidOperationException("Response is not JSON.");

            try
            {
                var json = Newtonsoft.Json.Linq.JObject.Parse(RawResponse);
                var token= json.SelectToken(jsonPath)?.ToString() ?? throw new InvalidOperationException($"JSON path '{jsonPath}' not found.");
                return ExtractRegexMatch(token, Pattern);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to extract JSON value using path '{jsonPath}'.", ex);
            }
        }

        public string ExtractXmlValue(string xpath)
        {
            if (Format != MimeType.TextXml &&
                   Format != MimeType.ApplicationXml &&
                   Format != MimeType.RawXml)
            {
                throw new InvalidOperationException("Response is not XML.");
            }

            try
            {
                var doc = new System.Xml.XmlDocument();
                doc.LoadXml(RawResponse);
                var node = doc.SelectSingleNode(xpath);
                return ExtractRegexMatch(node?.InnerText, Pattern) ?? throw new InvalidOperationException($"XPath '{xpath}' not found.");
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to extract XML value using XPath '{xpath}'.", ex);
            }
        }
        public string ExtractTextValue()
        {
            return ExtractRegexMatch(rawResponse, Pattern);
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
    }

}
