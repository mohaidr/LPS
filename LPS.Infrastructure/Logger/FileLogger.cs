
using System;
using System.IO;
using System.Threading.Tasks;
using System.Text;
using System.Threading;
using System.Reflection.Emit;
using LPS.Domain.Common.Interfaces;
using LPS.Infrastructure.Logging;
using System.Linq;
using System.Diagnostics;
using Microsoft.Extensions.Options;
using LPS.Infrastructure.Common.Interfaces;
using Spectre.Console;

namespace LPS.Infrastructure.Logger
{
    public class FileLogger : IFileLogger 
    {   
        private FileLogger() 
        { 
            this._logFilePath = "lps-logs.log";
            this._loggingLevel = LPSLoggingLevel.Verbose;
            this._consoleLoggingLevel = LPSLoggingLevel.Information;
            this._disableFileLogging = false;
            this._enableConsoleLogging = true;
            this._disableConsoleErrorLogging = false;
            SetLogFilePath(this._logFilePath);
            string directory = string.Concat($@"{Path.GetDirectoryName(_logFilePath)}");

            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
            string fileName = Path.GetFileName(_logFilePath);
            _synchronizedTextWriter = ObjectFactory.Instance.MakeSynchronizedTextWriter($@"{directory}\\{fileName}");
        }

        public FileLogger(string logFilePath, LPSLoggingLevel loggingLevel, LPSLoggingLevel consoleLoggingLevel, bool enableConsoleLogging = true, bool disableConsoleErrorLogging = true, bool disableFileLogging = false)
        {
            _loggingLevel = loggingLevel;
            _consoleLoggingLevel = consoleLoggingLevel;
            _enableConsoleLogging = enableConsoleLogging;
            _disableConsoleErrorLogging = disableConsoleErrorLogging;
            _disableFileLogging = disableFileLogging;
            SetLogFilePath(logFilePath);
            string directory = string.Concat($@"{Path.GetDirectoryName(_logFilePath)}");

            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
            string fileName = Path.GetFileName(_logFilePath);

            _synchronizedTextWriter = ObjectFactory.Instance.MakeSynchronizedTextWriter($@"{directory}\\{fileName}");
        }

        TextWriter _synchronizedTextWriter;

        public string LogFilePath { get { return _logFilePath; } }

        public bool EnableConsoleLogging { get { return _enableConsoleLogging; } }

        public bool DisableConsoleErrorLogging { get { return _disableConsoleErrorLogging; } }

        public bool DisableFileLogging { get { return _disableFileLogging; } }

        public LPSLoggingLevel ConsoleLoggingLevel { get { return _consoleLoggingLevel; } }

        public LPSLoggingLevel LoggingLevel { get { return _loggingLevel; } }

        private string _logFilePath;
        private bool _enableConsoleLogging;
        private bool _disableConsoleErrorLogging;
        private bool _disableFileLogging;
        private LPSLoggingLevel _consoleLoggingLevel;
        private LPSLoggingLevel _loggingLevel;
        private int _loggingCancellationCount;
        private string _cancellationErrorMessage;
        

        private void SetLogFilePath(string logFilePath)
        {
            if (string.IsNullOrWhiteSpace(logFilePath))
            {
                throw new ArgumentException("Log file path cannot be null or empty.", nameof(logFilePath));
            }

            // Check if the provided path contains a directory
            string directory = Path.GetDirectoryName(logFilePath);
            // If no directory is provided, use a default directory "logs"
            if (string.IsNullOrEmpty(directory))
            {
                directory = "logs";
                // Create the default directory if it doesn't exist
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }
            }
            else if (!Directory.Exists(directory))
            {
                // Create the specified directory if it doesn't exist
                Directory.CreateDirectory(directory);
            }

            // Combine the directory and file name to get the complete log file path
            _logFilePath = Path.Combine(directory, Path.GetFileName(logFilePath));
        }

        private static SemaphoreSlim semaphoreSlim = new SemaphoreSlim(1, 1);

        public void Log(string eventId, string diagnosticMessage, LPSLoggingLevel level, ICancellationTokenWrapper cancellationTokenWrapper = default)
        {
            var stopWatch = new Stopwatch();
            stopWatch.Start();
            LogAsync(eventId, diagnosticMessage, level, cancellationTokenWrapper).Wait();
            _ = LogAsync(eventId, $"Synchronous logging time was {stopWatch.Elapsed}", LPSLoggingLevel.Verbose, cancellationTokenWrapper);
            stopWatch.Stop();
        }

        public async Task LogAsync(string correlationId, string diagnosticMessage, LPSLoggingLevel level, ICancellationTokenWrapper cancellationTokenWrapper = default)
        {
            diagnosticMessage = Markup.Escape(diagnosticMessage);
            try
            {
                await semaphoreSlim.WaitAsync();
                string currentDateTime = DateTime.Now.ToString("dd-MM-yyyy HH:mm:ss.fff +3:00");
                if (level >= _consoleLoggingLevel && _enableConsoleLogging)
                {
                    if (level == LPSLoggingLevel.Verbose)
                    {

                        AnsiConsole.MarkupLine($"[blue]{level}:[/] {currentDateTime} {correlationId} {diagnosticMessage}");

                    }
                    else
                    if (level == LPSLoggingLevel.Information)
                    {
                        AnsiConsole.MarkupLine($"[blue]{level}:[/] {currentDateTime} {correlationId} {diagnosticMessage}");
                    }
                    else if (level == LPSLoggingLevel.Warning)
                    {
                        AnsiConsole.MarkupLine($"[yellow]{level}:[/] {currentDateTime} {correlationId} {diagnosticMessage}");

                    }
                    else if (level == LPSLoggingLevel.Error && !_disableConsoleErrorLogging)
                    {
                        AnsiConsole.MarkupLine($"[red]{level}:[/] {currentDateTime} {correlationId} {diagnosticMessage}");
                    }
                    else if (level == LPSLoggingLevel.Critical && !_disableConsoleErrorLogging)
                    {
                        AnsiConsole.MarkupLine($"[red]{level}:[/] {currentDateTime} {correlationId} {diagnosticMessage}");
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

                if (cancellationTokenWrapper!=default && cancellationTokenWrapper.CancellationToken.IsCancellationRequested && ex.Message != null && ex.Message.Equals("A task was canceled.", StringComparison.OrdinalIgnoreCase))
                {
                    _loggingCancellationCount++;
                    _cancellationErrorMessage = $"{ex.Message} {ex.InnerException?.Message}";

                }
                else
                {
                   AnsiConsole.MarkupLine($"[Yellow]Warning: Logging Failed  {ex.Message} {ex.InnerException?.Message}[/]");
                }
            }
            finally
            {
                semaphoreSlim.Release();
            }
        }

        public async Task Flush()
        {
            if (_loggingCancellationCount > 0)
            {
                AnsiConsole.MarkupLine($"[Yellow]The error '{_cancellationErrorMessage.Trim()}' has been reported {_loggingCancellationCount} times[/]");
            }
            await _synchronizedTextWriter.FlushAsync();
        }

        public static FileLogger GetDefaultInstance()
        { 
            return new FileLogger();
        }
    }
}
