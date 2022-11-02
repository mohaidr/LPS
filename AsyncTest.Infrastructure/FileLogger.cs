
using System;
using System.IO;
using System.Threading.Tasks;
using System.Text;
using System.Threading;
using System.Reflection.Emit;
using AsyncTest.Domain.Common;

namespace AsyncTest.Infrastructure.Logging
{
    public class FileLogger : IFileLogger
    {
        TextWriter SynchronizedTextWriter;
        public FileLogger()
        {
            if (String.IsNullOrEmpty(Location))
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("Logging has been disabled, Location Can't be empty");
                Console.ResetColor();
                return;
            }

            string directory = string.Concat($@"{Path.GetDirectoryName(Location)}");

            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
            string fileName = Path.GetFileName(Location);

            SynchronizedTextWriter = ObjectFactory.Instance.MakeSynchronizedTextWriter($@"{directory}\\{fileName}");

        }

        private static SemaphoreSlim semaphoreSlim = new SemaphoreSlim(1, 1);

        public string Location { get; set; } = "logs/load-test.Log";
        public void Log(string EventId, string DiagnosticMessage, LoggingLevel Level)
        {
            throw new NotImplementedException();
        }

        public async Task LogAsync(string EventId, string DiagnosticMessage, LoggingLevel Level)
        {
            try
            {
                await semaphoreSlim.WaitAsync();
                string currentDateTime = DateTime.Now.ToString("dd-MM-yyyy HH:mm:ss +3:00");
                string entry = currentDateTime + " " + Level + " " + DiagnosticMessage;
                await SynchronizedTextWriter.WriteLineAsync(entry);
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
