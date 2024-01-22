using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

    namespace LPS.Infrastructure.Common
    {
        public static class LPSSerializationHelper
        {
            private static readonly JsonSerializerOptions JsonSerializerOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = true
            };
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
        }
    }
