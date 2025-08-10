using LPS.Domain.Domain.Common.Enums;
using System;
using System.Collections.Generic;

 
namespace LPS.Domain.Domain.Common.Extensions
{

    public static class VariableTypeExtensions
    {
        private static readonly Dictionary<string, VariableType> Map =
            new(StringComparer.OrdinalIgnoreCase)
            {
                // String
                ["string"] = VariableType.String,
                ["text"] = VariableType.String,

                // Structured string shortcuts
                ["jsonstring"] = VariableType.JsonString,
                ["json"] = VariableType.JsonString,

                ["xmlstring"] = VariableType.XmlString,
                ["xml"] = VariableType.XmlString,

                ["csvstring"] = VariableType.CsvString,
                ["csv"] = VariableType.CsvString,

                // Numbers
                ["float"] = VariableType.Float,
                ["single"] = VariableType.Float,

                ["double"] = VariableType.Double,

                ["int"] = VariableType.Int,
                ["integer"] = VariableType.Int,

                ["decimal"] = VariableType.Decimal,

                // Boolean
                ["bool"] = VariableType.Boolean,
                ["boolean"] = VariableType.Boolean,

                // HttpResponse
                ["httpresponse"] = VariableType.HttpResponse,
                ["http-response"] = VariableType.HttpResponse,
                ["http_response"] = VariableType.HttpResponse,
                ["response"] = VariableType.HttpResponse
            };

        /// <summary>
        /// Tries to convert a string to <see cref="VariableType"/>.
        /// Accepts case-insensitive values. Also accepts "json", "xml", "csv" for JsonString/XmlString/CsvString.
        /// </summary>
        public static bool TryToVariableType(this string? value, out VariableType result)
        {
            result = default;

            if (string.IsNullOrWhiteSpace(value))
                return false;

            var key = value.Trim();

            return Map.TryGetValue(key, out result);
        }

        /// <summary>
        /// Converts a string to <see cref="VariableType"/> or throws if unsupported.
        /// </summary>
        public static VariableType ToVariableType(this string? value)
        {
            if (value.TryToVariableType(out var vt))
                return vt;

            throw new ArgumentException(
                $"Unsupported variable type '{value}'. " +
                "Supported: string, json, xml, csv, float, double, int, decimal, boolean, httpresponse.");
        }
    }
}
