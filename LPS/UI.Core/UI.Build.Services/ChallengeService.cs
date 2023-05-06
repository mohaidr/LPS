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
            Console.ForegroundColor = ConsoleColor.DarkCyan;
            string input;
            switch (challenge)
            {
                case "-testName":
                    Console.Write("Teast Name: ");
                    input = Console.ReadLine().Trim();
                    break;
                case "-numberOfClients":
                    Console.Write("Number Of Clients: ");
                    input = Console.ReadLine().Trim();
                    break;
                case "-clientTimeOut":
                    Console.Write("Client Timeout (Seconds): ");
                    input = Console.ReadLine().Trim();
                    break;                
                case "-rampupPeriod":
                    Console.Write("Rampup Period (Milliseconds): ");
                    input = Console.ReadLine().Trim();
                    break;
                case "-maxConnectionsPerServer":
                    Console.Write("Max Connections Per Server: ");
                    input = Console.ReadLine().Trim();
                    break;                
                case "-pooledConnectionLifeTime":
                    Console.Write("Pooled Connection Life time (Minutes): ");
                    input = Console.ReadLine().Trim();
                    break;
                case "-pooledConnectionIdleTimeout":
                    Console.Write("Pooled Connection Idle Timeout (Minutes): ");
                    input = Console.ReadLine().Trim();
                    break;
                case "-delayClientCreationUntilNeeded":
                    Console.Write("Dleay Client Creation Until Needed (Y/N): ");
                    input = Console.ReadLine().Trim();
                    break;
                case "-testCaseName":
                    Console.Write("Test Case Name: ");
                    input = Console.ReadLine().Trim();
                    break;
                case "-iterationMode":
                    Console.Write("Iteration Mode:");
                    input = Console.ReadLine().Trim();
                    break;
                case "-requestCount":
                    Console.Write("Request Count: ");
                    input = Console.ReadLine().Trim();
                    break;
                case "-duration":
                    Console.Write("Duration (Seconds): ");
                    input = Console.ReadLine().Trim();
                    break;
                case "-batchSize":
                    Console.Write("Batch Size: ");
                    input = Console.ReadLine().Trim();
                    break;
                case "-coolDownTime":
                    Console.Write("Cool Down Time (Seconds): ");
                    input = Console.ReadLine().Trim();
                    break;
                case "-httpversion":
                    Console.Write("Http Version: ");
                    input = Console.ReadLine().Trim();
                    break;
                case "-httpmethod":
                    Console.Write("Http Method: ");
                    input = Console.ReadLine().Trim();
                    break;
                case "-url":
                    Console.Write("Url: ");
                    input = Console.ReadLine().Trim();
                    break;
                default:
                    Console.ForegroundColor = ConsoleColor.Yellow; 
                        Console.WriteLine("Invalid Challenge");
                    Console.ResetColor();
                    input = string.Empty;
                    break;
            }

            Console.ResetColor();
            return input;
        }
    }
}
