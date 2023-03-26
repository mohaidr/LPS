using LPS.Extensions;
using System;
using System.Collections.Generic;
using System.CommandLine.Binding;
using System.Linq;
using System.Net.Http.Headers;

namespace LPS.UI.Core.UI.Build.Services
{
    internal class InputHeaderService
    {
        public static Dictionary<string, string> Challenge()
        {
            var kvp = new List<string>();

            Dictionary<string, string> httpheaders = new Dictionary<string, string>();
            while (true)
            {
                string input = Console.ReadLine().Trim();
                if (input == "done")
                {
                    break;
                }
                try
                {
                    kvp = input.Split(':').ToList<string>();
                    if (!httpheaders.ContainsKey(kvp.First().Trim()))
                        httpheaders.Add(kvp.First().Trim(), string.Join(":", kvp.Where(str => str != kvp.First())));
                    else
                        httpheaders[kvp[0]] = string.Join(":", kvp.Where(str => str != kvp.First()));
                }
                catch
                {
                    Console.WriteLine("Enter header in a valid format e.g (headerName: headerValue) or enter done to start filling the payload");
                }
            }
            return httpheaders.Clone();
        }

        public static Dictionary<string, string> Parse(IList<string> headers)
        {
            var kvp = new List<string>();
            Dictionary<string, string> httpheaders = new Dictionary<string, string>();

            foreach (string header in headers)
            {
                try
                {
                    kvp = header.Split(':').ToList<string>();
                    if (!httpheaders.ContainsKey(kvp.First().Trim()))
                        httpheaders.Add(kvp.First().Trim(), string.Join(":", kvp.Where(str => str != kvp.First())));
                    else
                        httpheaders[kvp.First()] = string.Join(":", kvp.Where(str => str != kvp.First()));
                }
                catch
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine($"Inavlid header: {header}");
                    Console.ResetColor();
                }
            }
            return httpheaders.Clone();
        }

    }
}
