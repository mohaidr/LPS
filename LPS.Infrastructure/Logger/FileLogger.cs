
using System;
using System.IO;
using System.Threading.Tasks;
using System.Text;
using System.Threading;
using System.Reflection.Emit;
using LPS.Domain.Common;
using LPS.Infrastructure.Logging;
using System.Linq;
using System.Diagnostics;
using Microsoft.Extensions.Options;

namespace LPS.Infrastructure.Logger
{
    public class FileLogger : IFileLogger
    {
        TextWriter _synchronizedTextWriter;

        public string LogFilePath { get { return _logFilePath; } }

        public bool EnableConsoleLogging { get { return _disableConsoleLogging; } }

        public bool DisableConsoleErrorLogging { get { return _disableConsoleErrorLogging; } }

        public bool DisableFileLogging { get { return _disableFileLogging; } }

        public LPSLoggingLevel ConsoleLoggingLevel { get { return _consoleLoggingLevel; } }

        public LPSLoggingLevel LoggingLevel { get { return _loggingLevel; } }

        private string _logFilePath;
        private bool _disableConsoleLogging;
        private bool _disableConsoleErrorLogging;
        private bool _disableFileLogging;
        private LPSLoggingLevel _consoleLoggingLevel;
        private LPSLoggingLevel _loggingLevel;

        public FileLogger(string logFilePath, LPSLoggingLevel loggingLevel, LPSLoggingLevel consoleLoggingLevel, bool disableConsoleLogging = true, bool disableConsoleErrorLogging = false, bool disableFileLogging = false)
        {
            _loggingLevel = loggingLevel;
            _consoleLoggingLevel = consoleLoggingLevel;
            _disableConsoleLogging = disableConsoleLogging;
            _disableConsoleErrorLogging = disableConsoleErrorLogging;
            _disableFileLogging = disableFileLogging;
            if (string.IsNullOrEmpty(logFilePath))
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("Logging has been disabled, Location Can't be empty");
                Console.ResetColor();
                return;
            }
            this._logFilePath = logFilePath;
            string directory = string.Concat($@"{Path.GetDirectoryName(_logFilePath)}");

            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
            string fileName = Path.GetFileName(_logFilePath);

            _synchronizedTextWriter = ObjectFactory.Instance.MakeSynchronizedTextWriter($@"{directory}\\{fileName}");
        }

        private static SemaphoreSlim semaphoreSlim = new SemaphoreSlim(1, 1);

        public void Log(string eventId, string diagnosticMessage, LPSLoggingLevel level, ICancellationTokenWrapper cancellationTokenWrapper = default)
        {
            var stopWatch = new Stopwatch();
            stopWatch.Start();
            LogAsync(eventId, diagnosticMessage, level, cancellationTokenWrapper).Wait();
            _ = LogAsync(eventId, $"Synchronous logging time was {stopWatch.Elapsed}", LPSLoggingLevel.Verbos, cancellationTokenWrapper);
            stopWatch.Stop();
        }

        public async Task LogAsync(string correlationId, string diagnosticMessage, LPSLoggingLevel level, ICancellationTokenWrapper cancellationTokenWrapper = default)
        {
            try
            {
                await semaphoreSlim.WaitAsync();
                string currentDateTime = DateTime.Now.ToString("dd-MM-yyyy HH:mm:ss.fff +3:00");
                if (level >= _consoleLoggingLevel && _disableConsoleLogging)
                {
                    if (level == LPSLoggingLevel.Verbos)
                    {
                        Console.ForegroundColor = ConsoleColor.DarkBlue;
                        Console.Write(level.ToString() + ": ");
                        Console.ResetColor();
                        Console.WriteLine($"{currentDateTime} {correlationId} {diagnosticMessage}");
                    }
                    else
                    if (level == LPSLoggingLevel.Information)
                    {
                        Console.ForegroundColor = ConsoleColor.Blue;
                        Console.Write(level.ToString() + ": ");
                        Console.ResetColor();
                        Console.WriteLine($"{currentDateTime} {correlationId} {diagnosticMessage}");
                    }
                    else if (level == LPSLoggingLevel.Warning)
                    {
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.Write(level.ToString() + ": ");
                        Console.ResetColor();
                        Console.WriteLine($"{currentDateTime} {correlationId} {diagnosticMessage}");
                    }
                    else if (level == LPSLoggingLevel.Error && !_disableConsoleErrorLogging)
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.Write(level.ToString() + " ");
                        Console.ResetColor();
                        Console.WriteLine($"{currentDateTime} {correlationId} {diagnosticMessage}");
                    }
                    else if (level == LPSLoggingLevel.Critical && _disableConsoleErrorLogging)
                    {
                        Console.ForegroundColor = ConsoleColor.DarkRed;
                        Console.Write(level.ToString() + " ");
                        Console.ResetColor();
                        Console.WriteLine($"{currentDateTime} {correlationId} {diagnosticMessage}");
                    }

                }
                if (!_disableFileLogging && level >= _loggingLevel)
                {
                    StringBuilder stringBuilder = new StringBuilder();
                    stringBuilder.Append($"{currentDateTime} {level} {correlationId} {diagnosticMessage}");
                    if (cancellationTokenWrapper != default)
                        await _synchronizedTextWriter.WriteLineAsync(stringBuilder, cancellationTokenWrapper.CancellationToken);
                    else
                        await _synchronizedTextWriter.WriteLineAsync(stringBuilder);

                }

            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"Warning: Logging Failed \n {ex.Message} {ex.InnerException?.Message}");
                Console.ResetColor();
            }
            finally
            {
                semaphoreSlim.Release();
            }
        }

        public async Task Flush()
        {
            await _synchronizedTextWriter.FlushAsync();
        }

    }
}
