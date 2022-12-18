using AsyncTest.Domain;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;

namespace AsyncCalls.UI.Core.UI.Build.Services
{
    internal static class CommandProps
    {

        public static Dictionary<string, bool> GetRequiredProperties()
        {
            Dictionary<string, bool> requiredProperties = new Dictionary<string, bool>()
            {
                { "-Add", false }, { "-a", false },
                { "-url",false },
                { "-httpmethod",false },{ "-hm",false },
                { "-header",false },{ "-h",false },
                { "-repeat",false }, { "-r",false }
            };
            return requiredProperties.ToDictionary(entry => entry.Key, entry => entry.Value);
        }

        public static Dictionary<string, bool> GetOptionalProperties()
        {

            Dictionary<string, bool> optionalProperties = new Dictionary<string, bool>()
            {
                { "-name", false },{ "-n", false },
                { "-payload", false },{ "-p", false },
                { "-requestname", false },{ "-rn", false },
                { "-httpversion", false }, { "-hv", false },
                { "-timeout", false },{ "-t", false },
            };
            return optionalProperties.ToDictionary(entry => entry.Key, entry => entry.Value);
        }

        public static void ResetProperties(Dictionary<string, bool> requiredProperties, Dictionary<string, bool> optionalProperties)
        {
            requiredProperties = GetRequiredProperties();
            optionalProperties = GetOptionalProperties();
        }
    }
}

