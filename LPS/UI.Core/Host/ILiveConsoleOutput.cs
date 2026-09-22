using Spectre.Console.Rendering;

namespace LPS.UI.Core.Host
{
    internal interface ILiveConsoleOutput
    {
        void Write(IRenderable message);
        void BeginLiveDisplay();
        void FlushPendingLogs();
        void EndLiveDisplay();
    }
}