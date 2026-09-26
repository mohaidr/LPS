using Spectre.Console;
using Spectre.Console.Rendering;
using System.Collections.Concurrent;

namespace LPS.UI.Core.Host
{
    internal sealed class LiveConsoleOutput(IAnsiConsole console, IAnsiConsole errorConsole) : ILiveConsoleOutput
    {
        private const int MaxLogsPerFrame = 128;
        private readonly object _outputLock = new();
        private readonly ConcurrentQueue<(IRenderable Message, bool StandardError)> _pendingLogs = new();
        private readonly Queue<IRenderable> _terminalErrors = new();
        private bool _liveDisplayActive;

        public void Write(IRenderable message, bool standardError = false)
        {
            lock (_outputLock)
            {
                if (_liveDisplayActive)
                {
                    _pendingLogs.Enqueue((message, standardError));
                }
                else
                {
                    (standardError ? errorConsole : console).Write(message);
                }
            }
        }

        public void BeginLiveDisplay()
        {
            lock (_outputLock)
            {
                _liveDisplayActive = true;
            }
        }

        public void FlushPendingLogs()
        {
            var count = Math.Min(_pendingLogs.Count, MaxLogsPerFrame);
            while (count-- > 0 && _pendingLogs.TryDequeue(out var entry))
            {
                if (_liveDisplayActive && entry.StandardError && errorConsole.Profile.Out.IsTerminal)
                    _terminalErrors.Enqueue(entry.Message);
                else
                    (entry.StandardError ? errorConsole : console).Write(entry.Message);
            }
        }

        public void EndLiveDisplay()
        {
            lock (_outputLock)
            {
                _liveDisplayActive = false;
                while (_terminalErrors.TryDequeue(out var message))
                    errorConsole.Write(message);
                while (_pendingLogs.TryDequeue(out var entry))
                    (entry.StandardError ? errorConsole : console).Write(entry.Message);
            }
        }
    }
}