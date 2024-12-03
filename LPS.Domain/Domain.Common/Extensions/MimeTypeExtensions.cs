using System;
using System.Collections.Generic;

namespace LPS.Domain.Common
{
    public enum MimeType
    {
        ImageJpeg,
        ImagePng,
        ApplicationPdf,
        TextPlain,
        ApplicationMsWord,
        ApplicationVndMsExcel,
        ApplicationVndOpenXmlFormatsOfficedocumentSpreadsheetmlSheet,
        ApplicationVndMsPowerpoint,
        ApplicationVndOpenXmlFormatsOfficedocumentPresentationmlPresentation,
        ApplicationXml,
        TextXml,
        RawXml,
        TextJavascript,
        ApplicationJavascript,
        ApplicationXJavascript,
        TextCss,
        TextHtml,
        ApplicationJson,
        TextCsv, // New enum value for CSV
        Unknown, // Enum value for unknown content types
    }

    public static class MimeTypeExtensions
    {
        private static readonly Dictionary<MimeType, string> MimeTypeToExtension = new()
        {
            { MimeType.ImageJpeg, ".jpg" },
            { MimeType.ImagePng, ".png" },
            { MimeType.ApplicationPdf, ".pdf" },
            { MimeType.TextPlain, ".txt" },
            { MimeType.ApplicationMsWord, ".doc" },
            { MimeType.ApplicationVndMsExcel, ".xls" },
            { MimeType.ApplicationVndOpenXmlFormatsOfficedocumentSpreadsheetmlSheet, ".xlsx" },
            { MimeType.ApplicationVndMsPowerpoint, ".ppt" },
            { MimeType.ApplicationVndOpenXmlFormatsOfficedocumentPresentationmlPresentation, ".pptx" },
            { MimeType.ApplicationXml, ".xml" },
            { MimeType.TextXml, ".xml" },
            { MimeType.RawXml, ".xml" },
            { MimeType.TextJavascript, ".js" },
            { MimeType.ApplicationJavascript, ".js" },
            { MimeType.ApplicationXJavascript, ".js" },
            { MimeType.TextCss, ".css" },
            { MimeType.TextHtml, ".html" },
            { MimeType.ApplicationJson, ".json" },
            { MimeType.TextCsv, ".csv" },
        };

        private static readonly Dictionary<string, MimeType> ContentTypeToMimeType = new(StringComparer.OrdinalIgnoreCase)
        {
            { "image/jpeg", MimeType.ImageJpeg },
            { "image/png", MimeType.ImagePng },
            { "application/pdf", MimeType.ApplicationPdf },
            { "text/plain", MimeType.TextPlain },
            { "application/msword", MimeType.ApplicationMsWord },
            { "application/vnd.ms-excel", MimeType.ApplicationVndMsExcel },
            { "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", MimeType.ApplicationVndOpenXmlFormatsOfficedocumentSpreadsheetmlSheet },
            { "application/vnd.ms-powerpoint", MimeType.ApplicationVndMsPowerpoint },
            { "application/vnd.openxmlformats-officedocument.presentationml.presentation", MimeType.ApplicationVndOpenXmlFormatsOfficedocumentPresentationmlPresentation },
            { "application/xml", MimeType.ApplicationXml },
            { "text/xml", MimeType.TextXml },
            { "application/raw+xml", MimeType.RawXml },
            { "text/javascript", MimeType.TextJavascript },
            { "application/javascript", MimeType.ApplicationJavascript },
            { "application/x-javascript", MimeType.ApplicationXJavascript },
            { "text/css", MimeType.TextCss },
            { "text/html", MimeType.TextHtml },
            { "application/json", MimeType.ApplicationJson },
            { "text/csv", MimeType.TextCsv }, // MIME type for CSV
        };

        private static readonly Dictionary<string, MimeType> KeywordToMimeType = new(StringComparer.OrdinalIgnoreCase)
        {
            { "JSON", MimeType.ApplicationJson },
            { "XML", MimeType.ApplicationXml },
            { "Text", MimeType.TextPlain },
            { "JPEG", MimeType.ImageJpeg },
            { "PNG", MimeType.ImagePng },
            { "HTML", MimeType.TextHtml },
            { "PDF", MimeType.ApplicationPdf },
            { "JS", MimeType.ApplicationJavascript },
            { "CSS", MimeType.TextCss },
            { "CSV", MimeType.TextCsv }, // Keyword for CSV
            // Add more mappings as needed
        };

        public static string ToFileExtension(this MimeType mimeType)
        {
            if (MimeTypeToExtension.TryGetValue(mimeType, out string extension))
            {
                return extension;
            }

            return ".bin"; // Default extension
        }

        public static MimeType FromContentType(string contentType)
        {
            if (contentType != null && ContentTypeToMimeType.TryGetValue(contentType, out MimeType mimeType))
            {
                return mimeType;
            }

            return MimeType.Unknown; // Default MIME type representing unknown content types
        }
        public static string ToContentType(this MimeType mimeType)
        {
            foreach (var kvp in ContentTypeToMimeType)
            {
                if (kvp.Value == mimeType)
                {
                    return kvp.Key;
                }
            }

            return "application/octet-stream"; // Default content type for unknown MIME types
        }
        public static MimeType FromKeyword(string keyword)
        {
            if (keyword != null && KeywordToMimeType.TryGetValue(keyword, out MimeType mimeType))
            {
                return mimeType;
            }

            return MimeType.Unknown; // Default MIME type representing unknown content types
        }
    }
}
