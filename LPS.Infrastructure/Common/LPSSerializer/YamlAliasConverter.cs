using LPS.Infrastructure.Common.LPSSerializer;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using YamlDotNet.Core;
using YamlDotNet.Serialization;

namespace LPS.Infrastructure.Common
{
    public class YamlAliasConverter : IYamlTypeConverter
    {
        public bool Accepts(Type type)
        {
            // Apply only to classes with alias attributes
            return type.IsClass && type.GetProperties().Any(p => p.IsDefined(typeof(YamlAliasAttribute), true));
        }

        public object ReadYaml(IParser parser, Type type, ObjectDeserializer deserializer)
        {
            // Deserialize into a temporary dictionary
            var dictionary = deserializer(typeof(Dictionary<string, object>)) as Dictionary<string, object>;
            var instance = Activator.CreateInstance(type);

            foreach (var prop in type.GetProperties())
            {
                // Automatically include the property name as an alias
                var aliases = prop.GetCustomAttributes(typeof(YamlAliasAttribute), true)
                                  .Cast<YamlAliasAttribute>()
                                  .Select(attr => attr.Alias)
                                  .ToList();

                aliases.Add(prop.Name); // Add the property name itself as an alias
                aliases = aliases.Select(alias => alias.ToLower()).Distinct().ToList(); // Ensure case-insensitivity and uniqueness

                // Find the first matching key
                var match = dictionary?.FirstOrDefault(kvp => aliases.Contains(kvp.Key.ToLower()));
                if (!string.IsNullOrEmpty(match?.Key) && match?.Value != null)
                {
                    try
                    {
                        var value = match?.Value;

                        // Check if the property type is a nested object
                        if (value is IDictionary<string, object>)
                        {
                            // Deserialize nested object using the default deserializer
                            var nestedObject = deserializer(prop.PropertyType);
                            prop.SetValue(instance, nestedObject);
                        }
                        else if (value != null && prop.PropertyType.IsAssignableFrom(value.GetType()))
                        {
                            // Direct assignment if types match
                            prop.SetValue(instance, value);
                        }
                        else if (value is IConvertible)
                        {
                            // Convert value to the target type
                            var convertedValue = Convert.ChangeType(value, prop.PropertyType);
                            prop.SetValue(instance, convertedValue);
                        }
                    }
                    catch (Exception ex)
                    {
                        throw new InvalidOperationException(
                            $"Failed to set property '{prop.Name}' with value '{match.Value}': {ex.Message}"
                        );
                    }
                }
            }

            return instance;
        }

        public void WriteYaml(IEmitter emitter, object value, Type type, ObjectSerializer serializer)
        {
            var dictionary = new Dictionary<string, object>();

            foreach (var prop in type.GetProperties())
            {
                // Use the first alias or default to the property name
                var alias = prop.GetCustomAttributes(typeof(YamlAliasAttribute), true)
                                .Cast<YamlAliasAttribute>()
                                .Select(attr => attr.Alias)
                                .FirstOrDefault() ?? prop.Name;

                var propValue = prop.GetValue(value);
                if (propValue != null)
                {
                    dictionary[alias] = propValue;
                }
            }

            serializer(dictionary);
        }
    }

}