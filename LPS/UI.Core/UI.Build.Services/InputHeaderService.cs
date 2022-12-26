using LPS.Extensions;
using System;
using System.Collections.Generic;

namespace LPS.UI.Core.UI.Build.Services
{
    internal class InputHeaderService
    {
        public static Dictionary<string, string> Challenge()
        {
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
                    string[] header = input.Split(':');
                    if (!HttpHeaders.ContainsKey(header[0].Trim()))
                        HttpHeaders.Add(header[0].Trim(), header[1].Trim());
                    else
                        HttpHeaders[header[0].Trim()] = header[1].Trim();
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
