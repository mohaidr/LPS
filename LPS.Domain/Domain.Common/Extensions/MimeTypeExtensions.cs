using System;
using System.Collections.Generic;

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
    TextJavascript,
    ApplicationJavascript,
    ApplicationXJavascript,
    TextCss,
    TextHtml,
    ApplicationJson,
    Unknown, // New enum value for unknown content types
    // Add more MIME types as needed
}

public static class MimeTypeExtensions
{
    private static readonly Dictionary<MimeType, string> MimeTypeToExtension = new Dictionary<MimeType, string>
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
        { MimeType.TextJavascript, ".js" },
        { MimeType.ApplicationJavascript, ".js" },
        { MimeType.ApplicationXJavascript, ".js" },
        { MimeType.TextCss, ".css" },
        { MimeType.TextHtml, ".html" },
        { MimeType.ApplicationJson, ".json" },
        // Add more mappings as needed
    };

    private static readonly Dictionary<string, MimeType> ContentTypeToMimeType = new Dictionary<string, MimeType>
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
        { "text/javascript", MimeType.TextJavascript },
        { "application/javascript", MimeType.ApplicationJavascript },
        { "application/x-javascript", MimeType.ApplicationXJavascript },
        { "text/css", MimeType.TextCss },
        { "text/html", MimeType.TextHtml },
        { "application/json", MimeType.ApplicationJson },
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
        if (ContentTypeToMimeType.TryGetValue(contentType, out MimeType mimeType))
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
}
