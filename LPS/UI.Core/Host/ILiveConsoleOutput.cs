using Spectre.Console.Rendering;

namespace LPS.UI.Core.Host
{
    internal interface ILiveConsoleOutput
    {
        void Write(IRenderable message, bool standardError = false);
        void BeginLiveDisplay();
        void FlushPendingLogs();
        void EndLiveDisplay();
    }
}