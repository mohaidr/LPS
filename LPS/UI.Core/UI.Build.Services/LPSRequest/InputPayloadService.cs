using LPS.Domain;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace LPS.UI.Core.UI.Build.Services
{
    internal class InputPayloadService
    {
        public enum PayloadFrom
        { 
            File,
            Console
        }

        public static PayloadFrom payloadFrom { get; set; }

        public static string ReadFromFile(string path)
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

        public static string Challenge()
        {
            if (payloadFrom == PayloadFrom.File)
            {
                Console.WriteLine("Where should we read the payload from?");
                return ReadFromFile(Console.ReadLine());
            }
            else
            {
                Console.WriteLine("Payload");
                return Console.ReadLine();
            }    
        }
    }
}
