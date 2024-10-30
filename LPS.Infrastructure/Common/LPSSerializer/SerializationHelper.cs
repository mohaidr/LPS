using System;
using System.Linq;
using System.Text.Json;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace LPS.Infrastructure.Common
{
    public static class SerializationHelper
    {
        private static readonly JsonSerializerOptions JsonSerializerOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        };

        // YAML Serializer and Deserializer with camelCase convention
        private static readonly ISerializer YamlSerializer = new SerializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitDefaults)
            .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitNull)
            .Build();

        private static readonly IDeserializer YamlDeserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .Build();

        public static bool IsSerializable<T>(Type type = null)
        {
            type = type ?? typeof(T);
            // Check for the [Serializable] attribute
            if (IsSerializableAttribute(type))
            {
                return true;
            }

            try
            {
                // Attempt to serialize an instance of the type
                object obj = Activator.CreateInstance(type);
                string jsonString = JsonSerializer.Serialize(obj);
                return true;
            }
            catch (Exception)
            {
                // Serialization failed or the [Serializable] attribute is not present
                return false;
            }
        }

        private static bool IsSerializableAttribute(Type type)
        {
            // Check if the type is marked as Serializable
            return type.GetCustomAttributes(typeof(SerializableAttribute), true).Any();
        }

        public static string Serialize<T>(T obj)
        {
            try
            {
                return JsonSerializer.Serialize<T>(obj, JsonSerializerOptions);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Serialization Has Failed {ex.Message}");
            }
        }

        public static T Deserialize<T>(string jsonString)
        {
            try
            {
                return JsonSerializer.Deserialize<T>(jsonString, JsonSerializerOptions);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Serialization Has Failed {ex.Message}");
            }
        }

        // New Method: Serialize to YAML
        public static string SerializeToYaml<T>(T obj)
        {
            try
            {
                return YamlSerializer.Serialize(obj);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"YAML Serialization Has Failed: {ex.Message}");
            }
        }

        // New Method: Deserialize from YAML
        public static T DeserializeFromYaml<T>(string yamlString)
        {
            try
            {
                return YamlDeserializer.Deserialize<T>(yamlString);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"YAML Deserialization Has Failed: {ex.Message}");
            }
        }
    }
}
