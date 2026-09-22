using Spectre.Console;
using Spectre.Console.Rendering;
using System.Collections.Concurrent;

namespace LPS.UI.Core.Host
{
    internal sealed class LiveConsoleOutput(IAnsiConsole console) : ILiveConsoleOutput
    {
        private readonly object _outputLock = new();
        private readonly ConcurrentQueue<IRenderable> _pendingLogs = new();
        private bool _liveDisplayActive;

        public void Write(IRenderable message)
        {
            lock (_outputLock)
            {
                if (_liveDisplayActive)
                {
                    _pendingLogs.Enqueue(message);
                }
                else
                {
                    console.Write(message);
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
            while (_pendingLogs.TryDequeue(out var message))
            {
                console.Write(message);
            }
        }

        public void EndLiveDisplay()
        {
            lock (_outputLock)
            {
                _liveDisplayActive = false;
                FlushPendingLogs();
            }
        }
    }
}