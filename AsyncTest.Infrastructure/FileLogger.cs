using AsyncTest.Domain;
using System;
using System.IO;
using AsyncTest.Domain.Common;
using System.Threading.Tasks;
using System.Text;
using System.Threading;

namespace AsyncTest.Infrastructure
{
    public class FileLogger : IFileLogger
    {

        public FileLogger()
        { 
        
        }

        private static SemaphoreSlim semaphoreSlim = new SemaphoreSlim(1, 1);

        // Change the implementation to accept the location and file name as constructor parameters
        public string Location { get; set; } = $@"{DateTime.Now.Ticks.ToString() }\\{DateTime.Now.Ticks.ToString()}.json";
        public void Log(string EventId, string DiagnosticMessage, LoggingLevel Level)
        {
            throw new NotImplementedException();
        }

        public async Task LogAsync(string EventId, string DiagnosticMessage, LoggingLevel Level)
        {
            try
            {
                if (String.IsNullOrEmpty(Location) || !(Location.EndsWith(".txt") || Location.EndsWith(".json")))
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine("Logging has been disabled due to one of the below reason \n  Incorrect location. \n  File name does not end with .txt|.Json");
                    Console.ResetColor();
                    return;
                }
                string directory = string.Concat($@"{Path.GetDirectoryName(Location)}\\{Level}");

                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }
                string fileName = Path.GetFileName(Location);

                TextWriter SynchronizedTextWriter = ObjectFactory.Instance.MakeSynchronizedTextWriter($@"{directory}\\{fileName}");

                string encodedText = System.Web.HttpUtility.HtmlEncode(DiagnosticMessage);

                string entry = string.Empty;

                entry = String.Concat(entry, "{\n");
                entry = String.Concat(entry, ($"\t\"Log_Level\": \"{Level.ToString()}\",\n"));
                if (!string.IsNullOrEmpty(EventId))
                    entry = string.Concat(entry, ($"\t\"Event_Id\": \"{EventId}\",\n"));

                if (!string.IsNullOrEmpty(DiagnosticMessage))
                    entry = string.Concat(entry, ($"\t\"Message\": \"{encodedText}\"\n"));

                entry = string.Concat(entry, "},\n");
                await semaphoreSlim.WaitAsync();
                await SynchronizedTextWriter.WriteLineAsync(entry);
                await SynchronizedTextWriter.FlushAsync();
                semaphoreSlim.Release();
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"Warning: Logging Failed \n {ex.Message}");
                Console.ResetColor();
            }
        }
    }
}
