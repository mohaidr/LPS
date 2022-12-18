using AsyncTest.Domain;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace AsyncCalls.UI.Core.UI.Build.Services
{
    internal class ChallengeService
    {
        public static void SetOptionalFeildsToDefaultValues(HttpAsyncRequestWrapper.SetupCommand httpRequestWrapperDto)
        {
            httpRequestWrapperDto.Name = !string.IsNullOrEmpty(httpRequestWrapperDto.Name) ? httpRequestWrapperDto.Name : DateTime.Now.Ticks.ToString();
            httpRequestWrapperDto.HttpRequest.TimeOut = httpRequestWrapperDto.HttpRequest.TimeOut == 0 ? 4 : httpRequestWrapperDto.HttpRequest.TimeOut;
            httpRequestWrapperDto.HttpRequest.Httpversion = httpRequestWrapperDto.HttpRequest.Httpversion ?? "1.1";
        }

        //work on this to receive userInputObject(create a child calss)

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
                case "-repeat":
                    Console.Write("Repeat: ");
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
