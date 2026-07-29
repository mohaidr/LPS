#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Playwright;

namespace LPS.UI.Core.Recording
{
    /// <summary>
    /// Launches Chromium via Playwright and records all traffic to a HAR file using Playwright's
    /// native HAR recording (which buffers everything internally — no per-request concurrency code).
    /// Recording stops on the FIRST of: browser/page closed, Enter pressed, or cancellation.
    /// The HAR is flushed to disk when the context is closed.
    /// </summary>
    internal sealed class PlaywrightRecorder
    {
        public async Task RecordAsync(string harPath, string? startUrl, bool headless, CancellationToken cancellationToken)
        {
            using var playwright = await Playwright.CreateAsync();

            await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
            {
                Headless = headless
            });

            var context = await browser.NewContextAsync(new BrowserNewContextOptions
            {
                RecordHarPath = harPath,
                RecordHarContent = HarContentPolicy.Embed
            });

            var stop = new TaskCompletionSource();
            void TriggerStop() => stop.TrySetResult();

            browser.Disconnected += (_, _) => TriggerStop();
            context.Close += (_, _) => TriggerStop();

            using var registration = cancellationToken.Register(TriggerStop);

            var page = await context.NewPageAsync();
            page.Close += (_, _) => TriggerStop();

            if (!string.IsNullOrWhiteSpace(startUrl))
            {
                try { await page.GotoAsync(startUrl); }
                catch { /* invalid/unreachable start URL — the user can still navigate manually */ }
            }

            // Poll for the Enter key (rather than a blocking ReadLine) so stdin stays clean for any
            // post-recording prompts, and this loop exits promptly if recording is stopped another way.
            _ = Task.Run(async () =>
            {
                try
                {
                    while (!stop.Task.IsCompleted)
                    {
                        if (Console.KeyAvailable && Console.ReadKey(intercept: true).Key == ConsoleKey.Enter)
                        {
                            TriggerStop();
                            return;
                        }
                        await Task.Delay(150);
                    }
                }
                catch { /* input redirected / no interactive console */ }
            });

            await stop.Task;

            // Closing the context is what flushes the HAR file. Guard in case the browser
            // window was already closed by the user (which disposes the context for us).
            try { await context.CloseAsync(); } catch { /* already closed */ }
            try { await browser.CloseAsync(); } catch { /* already closed */ }
        }
    }
}
