using LPS.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;

namespace LPS.UI.Core.UI.Build.Services
{
    internal class InputHeaderService
    {
        public static Dictionary<string, string> Challenge()
        {
            var kvp = new List<string>();

            Dictionary<string, string> HttpHeaders = new Dictionary<string, string>();
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
                    if (!HttpHeaders.ContainsKey(kvp.First().Trim()))
                        HttpHeaders.Add(kvp.First().Trim(), string.Join(":", kvp.Where(str => str != kvp.First())));
                    else
                        HttpHeaders[kvp[0]] = string.Join(":", kvp.Where(str => str != kvp.First()));
                }
                catch
                {
                    Console.WriteLine("Enter header in a valid format e.g (headerName: headerValue) or enter done to start filling the payload");
                }
            }
            return HttpHeaders.Clone();
        }
    }
}
