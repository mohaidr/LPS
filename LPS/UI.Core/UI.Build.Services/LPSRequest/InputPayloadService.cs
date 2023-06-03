using LPS.Domain;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;

namespace LPS.UI.Core.UI.Build.Services
{
    internal class InputPayloadService
    {

        private static string ReadFromFile(string path)
        {
            bool loop = true;
            string payload = string.Empty;
            while (loop)
            {
                try
                {
                    payload = File.ReadAllText(path);
                }
                catch (Exception ex)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("Unable To Read Data From The Specified Path");
                    Console.WriteLine(ex.Message);
                    Console.ResetColor();

                    Console.WriteLine("Would you like to retry to read the payload? (Y) to retry, (N) to cancel the test, (C) to continue without payload");
                    string retryDecision = Console.ReadLine();

                    switch (retryDecision.Trim().ToLower())
                    {
                        case "y":
                            break;
                        case "n":
                            throw new Exception("Test Cancelled");
                        case "c":
                            loop = false;
                            break;
                    }
                }

                break;
            }
            return payload;
        }

        private static string ReadFromURL(string url)
        {
            bool loop = true;
            string payload = string.Empty;
            while (loop)
            {
                try
                {
                    using (HttpClient client = new HttpClient())
                    {
                        payload = client.GetStringAsync(url).Result;
                    }
                }
                catch (Exception ex)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("Unable To Read Data From The Specified URL");
                    Console.WriteLine(ex.Message);
                    Console.ResetColor();

                    Console.WriteLine("Would you like to retry to read the payload? (Y) to retry, (N) to cancel the test, (C) to continue without payload");
                    string retryDecision = Console.ReadLine();

                    switch (retryDecision.Trim().ToLower())
                    {
                        case "y":
                            break;
                        case "n":
                            throw new Exception("Test Cancelled");
                        case "c":
                            loop = false;
                            break;
                    }
                }

                break;
            }
            return payload;
        }

        public static string Parse(string input)
        {
            if (input.StartsWith("URL:"))
            {
                return ReadFromURL(input.Substring(4));
            }
            else if (input.StartsWith("Path:"))
            {
                return ReadFromFile(input.Substring(5));
            }
            else
            {
                return input;
            }
        }

        public static string Challenge()
        {
            Console.WriteLine("Where should we read the payload from? URL:[URL] to reald from a web page, Path:[Path] to read from a local file or just provide an inline payload");
            string input = Console.ReadLine();

            // Parse the input
            return Parse(input);

        }
    }
}
