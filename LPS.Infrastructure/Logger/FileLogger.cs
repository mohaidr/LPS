
using System;
using System.IO;
using System.Threading.Tasks;
using System.Text;
using System.Threading;
using System.Reflection.Emit;
using LPS.Domain.Common;
using LPS.Infrastructure.Logging;
using System.Linq;

namespace LPS.Infrastructure.Logger
{
    public class FileLogger : IFileLogger
    {
        TextWriter SynchronizedTextWriter;
        public FileLogger(string logFilePath)
        {
            if (string.IsNullOrEmpty(logFilePath))
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("Logging has been disabled, Location Can't be empty");
                Console.ResetColor();
                return;
            }
            this.LogFilePath= logFilePath;
            string directory = string.Concat($@"{Path.GetDirectoryName(LogFilePath)}");

            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
            string fileName = Path.GetFileName(LogFilePath);

            SynchronizedTextWriter = ObjectFactory.Instance.MakeSynchronizedTextWriter($@"{directory}\\{fileName}");
        }

        private static SemaphoreSlim semaphoreSlim = new SemaphoreSlim(1, 1);

        private string LogFilePath { get; set; }
        public bool EnableConsoleLogging { get; set; } 
        public LPSLoggingLevel ConsoleLoggingLevel { get; set; }
        public LPSLoggingLevel LoggingLevel { get; set; }
        public void Log(string eventId, string diagnosticMessage, LPSLoggingLevel level)
        {
            LogAsync(eventId, diagnosticMessage, LPSLoggingLevel.Warning).Wait();
        }

        public async Task LogAsync(string correlationId, string diagnosticMessage, LPSLoggingLevel level)
        {
            try
            {
                await semaphoreSlim.WaitAsync();
                string currentDateTime = DateTime.Now.ToString("dd-MM-yyyy HH:mm:ss +3:00");
                if (level>= ConsoleLoggingLevel && EnableConsoleLogging)
                {
                    if (level == LPSLoggingLevel.Information)
                    {
                        Console.ForegroundColor = ConsoleColor.Blue;
                        Console.Write(level.ToString() + ": ");
                        Console.ResetColor();
                        Console.WriteLine($"{currentDateTime} {correlationId} {diagnosticMessage}");
                    }else if (level == LPSLoggingLevel.Warning)
                    {
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.Write(level.ToString() + ": ");
                        Console.ResetColor();
                        Console.WriteLine($"{currentDateTime} {correlationId} {diagnosticMessage}");
                    }
                    else if (level == LPSLoggingLevel.Error)
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.Write(level.ToString()+" ");
                        Console.ResetColor();
                        Console.WriteLine($"{currentDateTime} {correlationId} {diagnosticMessage}");
                    }
                    else if (level == LPSLoggingLevel.Critical)
                    {
                        Console.ForegroundColor = ConsoleColor.DarkRed;
                        Console.Write(level.ToString() + " ");
                        Console.ResetColor();
                        Console.WriteLine($"{currentDateTime} {correlationId} {diagnosticMessage}");
                    }

                }
                await SynchronizedTextWriter.WriteLineAsync($"{currentDateTime} {level} {correlationId} {diagnosticMessage}");
                semaphoreSlim.Release();
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"Warning: Logging Failed \n {ex.Message}");
                Console.ResetColor();
            }
        }

        public async Task Flush()
        {
            await SynchronizedTextWriter.FlushAsync();
        }

    }
}
