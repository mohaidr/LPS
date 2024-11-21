using LPS.Domain.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LPS.Infrastructure.LPSClients.SessionManager
{
    public class CapturedResponse(string rawResponse, string format) : ICapturedResponse
    {
        public string RawResponse { get; private set; } = rawResponse ?? throw new ArgumentNullException(nameof(rawResponse));
        public string Format { get; private set; } = format ?? throw new ArgumentNullException(nameof(format));

        public string ExtractJsonValue(string jsonPath)
        {
            if (Format != MimeType.ApplicationJson.ToString())
                throw new InvalidOperationException("Response is not JSON.");

            try
            {
                var json = Newtonsoft.Json.Linq.JObject.Parse(RawResponse);
                return json.SelectToken(jsonPath)?.ToString() ?? throw new InvalidOperationException($"JSON path '{jsonPath}' not found.");
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to extract JSON value using path '{jsonPath}'.", ex);
            }
        }

        public string ExtractRegexMatch(string pattern)
        {
            try
            {
                var match = System.Text.RegularExpressions.Regex.Match(RawResponse, pattern);
                return match.Success ? match.Value : throw new InvalidOperationException($"Pattern '{pattern}' did not match.");
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to apply regex pattern '{pattern}'.", ex);
            }
        }

        public string ExtractXmlValue(string xpath)
        {
            if (Format != MimeType.TextXml.ToString() &&
                   Format != MimeType.ApplicationXml.ToString() &&
                   Format != MimeType.RawXml.ToString())
            {
                throw new InvalidOperationException("Response is not XML.");
            }

            try
            {
                var doc = new System.Xml.XmlDocument();
                doc.LoadXml(RawResponse);
                var node = doc.SelectSingleNode(xpath);
                return node?.InnerText ?? throw new InvalidOperationException($"XPath '{xpath}' not found.");
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to extract XML value using XPath '{xpath}'.", ex);
            }
        }
    }

}
