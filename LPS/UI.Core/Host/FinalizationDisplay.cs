using Spectre.Console;
using System.Diagnostics;

namespace LPS.UI.Core.Host
{
    internal sealed class FinalizationDisplay(IAnsiConsole console, ILiveConsoleOutput output) : IFinalizationDisplay
    {
        private readonly TaskCompletionSource<TimeSpan> _started = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public void Start(TimeSpan estimatedDuration)
        {
            _started.TrySetResult(estimatedDuration);
        }

        public async Task ShowUntilShutdownAsync(Task shutdownTask)
        {
            await Task.WhenAny(_started.Task, shutdownTask);
            if (!_started.Task.IsCompleted)
            {
                await shutdownTask;
                return;
            }

            var estimatedDuration = await _started.Task;
            if (!console.Profile.Out.IsTerminal || !console.Profile.Capabilities.Interactive
                || !console.Profile.Capabilities.Ansi)
            {
                await shutdownTask;
                console.WriteLine("Finalization complete.");
                return;
            }

            var stopwatch = Stopwatch.StartNew();
            output.BeginLiveDisplay();
            try
            {
                await console.Live(FinalizationFrameRenderer.Render(0, 0, console.Profile.Width))
                    .AutoClear(false)
                    .StartAsync(async context =>
                    {
                        while (!shutdownTask.IsCompleted)
                        {
                            output.FlushPendingLogs();
                            var elapsed = stopwatch.Elapsed;
                            var completion = Math.Min(elapsed.TotalMilliseconds / Math.Max(1000, estimatedDuration.TotalMilliseconds), 0.95);
                            context.UpdateTarget(FinalizationFrameRenderer.Render(completion, (int)(elapsed.TotalMilliseconds / 50), console.Profile.Width));
                            await Task.WhenAny(Task.Delay(50), shutdownTask);
                        }

                        await shutdownTask;
                        output.FlushPendingLogs();
                        context.UpdateTarget(FinalizationFrameRenderer.Render(1, 0, console.Profile.Width));
                    });
            }
            finally
            {
                output.EndLiveDisplay();
            }
        }
    }
}