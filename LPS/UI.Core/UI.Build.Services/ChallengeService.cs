using LPS.Domain;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace LPS.UI.Core.UI.Build.Services
{
    internal class ChallengeService
    {
        public static string Challenge(string challenge)
        {

            switch (challenge)
            {
                case "-testName":
                    Console.Write("Teast Name: ");
                    return Console.ReadLine().Trim();
                case "-requestname":
                    Console.Write("Request Name: ");
                    return Console.ReadLine().Trim();
                case "-httpversion":
                    Console.Write("Http Version: ");
                    return Console.ReadLine().Trim();
                case "-httpmethod":
                    Console.Write("Http Method: ");
                    return Console.ReadLine().Trim();
                case "-timeout":
                    Console.Write("Timeout: ");
                    return Console.ReadLine().Trim();
                case "-requestCount":
                    Console.Write("Request Count: ");
                    return Console.ReadLine().Trim();
                case "-url":
                    Console.Write("Url: ");
                    return Console.ReadLine().Trim();
                    default:
                    Console.WriteLine("Invalid Challenge");
                    return string.Empty;
            }
        }
    }
}
