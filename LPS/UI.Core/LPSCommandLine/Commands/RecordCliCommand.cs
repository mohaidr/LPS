#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.CommandLine;
using System.CommandLine.Invocation;
using LPS.Domain.Common.Interfaces;
using LPS.UI.Common;
using LPS.UI.Common.DTOs;
using LPS.UI.Common.Extensions;
using LPS.UI.Core.LPSValidators;
using LPS.UI.Core.Recording;
using LPS.UI.Core.Recording.Har;
using LPS.UI.Core.Services;
using Microsoft.Playwright;
using static LPS.UI.Core.LPSCommandLine.CommandLineOptions;

namespace LPS.UI.Core.LPSCommandLine.Commands
{
    /// <summary>
    /// `lps record` — launches a browser (Playwright), records the traffic as the user browses,
    /// filters it, and writes a starting test plan (YAML/JSON). `lps record --install` provisions
    /// the Chromium binaries.
    /// </summary>
    internal class RecordCliCommand : ICliCommand
    {
        private readonly Command _rootCliCommand;
        private readonly ILogger _logger;
        private readonly IRuntimeOperationIdProvider _runtimeOperationIdProvider;
        private Command _recordCommand;

        public Command Command => _recordCommand;

#pragma warning disable CS8618 // _recordCommand is initialized in Setup()
        internal RecordCliCommand(
#pragma warning restore CS8618
            Command rootCliCommand,
            ILogger logger,
            IRuntimeOperationIdProvider runtimeOperationIdProvider)
        {
            _rootCliCommand = rootCliCommand;
            _logger = logger;
            _runtimeOperationIdProvider = runtimeOperationIdProvider;
            Setup();
        }

        private void Setup()
        {
            _recordCommand = new Command("record", "Record browser traffic via Playwright and generate a test plan");
            AddOptionsToCommand(_recordCommand, typeof(LPSRecordCommandOptions));
            _recordCommand.AddArgument(LPSRecordCommandOptions.OutputFileArgument);
            _rootCliCommand.AddCommand(_recordCommand);
        }

        public void SetHandler(CancellationToken cancellationToken)
        {
            _recordCommand.SetHandler(async (InvocationContext ctx) =>
            {
                try
                {
                    var parse = ctx.ParseResult;

                    if (parse.GetValueForOption(LPSRecordCommandOptions.InstallOption))
                    {
                        InstallBrowsers();
                        return;
                    }

                    var output = parse.GetValueForArgument(LPSRecordCommandOptions.OutputFileArgument) ?? "plan.yaml";
                    var planName = parse.GetValueForOption(LPSRecordCommandOptions.PlanNameOption) ?? "RecordedPlan";
                    var startUrl = parse.GetValueForOption(LPSRecordCommandOptions.UrlOption);
                    var headless = parse.GetValueForOption(LPSRecordCommandOptions.HeadlessOption);
                    var ignoreContentTypes = parse.GetValueForOption(LPSRecordCommandOptions.IgnoreContentTypeOption);
                    var ignoreExtensions = parse.GetValueForOption(LPSRecordCommandOptions.IgnoreExtensionOption);
                    var ignoreMethods = parse.GetValueForOption(LPSRecordCommandOptions.IgnoreMethodOption);
                    var ignoreResourceTypes = parse.GetValueForOption(LPSRecordCommandOptions.IgnoreResourceTypeOption);
                    var onlyHosts = parse.GetValueForOption(LPSRecordCommandOptions.OnlyHostOption);
                    var ignoreHosts = parse.GetValueForOption(LPSRecordCommandOptions.IgnoreHostOption);
                    var ignorePaths = parse.GetValueForOption(LPSRecordCommandOptions.IgnorePathOption);

                    var harPath = Path.Combine(Path.GetTempPath(), $"lps-record-{Guid.NewGuid():N}.har");

                    Console.WriteLine("Launching browser... browse your app, then close the browser window or press Enter here to finish.");

                    var recorder = new PlaywrightRecorder();
                    try
                    {
                        await recorder.RecordAsync(harPath, startUrl, headless, cancellationToken);
                    }
                    catch (PlaywrightException pex) when (pex.Message.Contains("Executable doesn't exist", StringComparison.OrdinalIgnoreCase)
                                                          || pex.Message.Contains("playwright install", StringComparison.OrdinalIgnoreCase))
                    {
                        _logger.Log(_runtimeOperationIdProvider.OperationId,
                            "Playwright browser is not installed. Run 'lps record --install' first, then try again.",
                            LPSLoggingLevel.Error);
                        return;
                    }

                    if (!File.Exists(harPath))
                    {
                        _logger.Log(_runtimeOperationIdProvider.OperationId,
                            "No traffic was captured (the HAR file was not created).", LPSLoggingLevel.Warning);
                        return;
                    }

                    HarRoot? har;
                    try
                    {
                        var json = await File.ReadAllTextAsync(harPath, cancellationToken);
                        har = JsonSerializer.Deserialize<HarRoot>(json, new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true
                        });
                    }
                    finally
                    {
                        TryDelete(harPath);
                    }

                    var promptFiles = parse.GetValueForOption(LPSRecordCommandOptions.PromptFilesOption);

                    var filter = new CaptureFilter(
                        ignoreContentTypes, ignoreExtensions, ignoreMethods, ignoreResourceTypes,
                        onlyHosts, ignoreHosts, ignorePaths);
                    var conversion = new HarToPlanConverter().Convert(har ?? new HarRoot(), filter, planName);
                    var plan = conversion.Plan;

                    var capturedCount = har?.Log?.Entries?.Count ?? 0;
                    var iterationCount = plan.Rounds.FirstOrDefault()?.Iterations.Count ?? 0;
                    if (iterationCount == 0)
                    {
                        _logger.Log(_runtimeOperationIdProvider.OperationId,
                            $"Captured {capturedCount} request(s) but 0 remained after filtering — nothing was written. Check your --only-host / --ignore-* filters.",
                            LPSLoggingLevel.Warning);
                        return;
                    }

                    if (conversion.FileUploads.Count > 0)
                    {
                        if (promptFiles)
                            PromptForFilePaths(conversion.FileUploads);
                        else
                            ReportFileUploads(conversion.FileUploads);
                    }

                    var validation = new PlanValidator(plan).Validate();
                    if (!validation.IsValid)
                    {
                        validation.PrintValidationErrors();
                    }

                    ConfigurationService.SaveConfiguration(output, plan);

                    _logger.Log(_runtimeOperationIdProvider.OperationId,
                        $"Recorded {iterationCount} request(s) into '{output}'.", LPSLoggingLevel.Information);
                }
                catch (Exception ex)
                {
                    _logger.Log(_runtimeOperationIdProvider.OperationId,
                        $"{ex.Message}\r\n{ex.InnerException?.Message}\r\n{ex.StackTrace}", LPSLoggingLevel.Error);
                }
            });
        }

        private void InstallBrowsers()
        {
            Console.WriteLine("Installing the Playwright Chromium browser...");
            var exitCode = Microsoft.Playwright.Program.Main(new[] { "install", "chromium" });
            if (exitCode == 0)
            {
                Console.WriteLine("Playwright Chromium installed.");
            }
            else
            {
                _logger.Log(_runtimeOperationIdProvider.OperationId,
                    $"Playwright install exited with code {exitCode}.", LPSLoggingLevel.Warning);
            }
        }

        private void PromptForFilePaths(IReadOnlyList<PendingFileUpload> uploads)
        {
            Console.WriteLine();
            Console.WriteLine($"{uploads.Count} file upload(s) were captured. Enter a local path for each (blank keeps the placeholder):");
            foreach (var upload in uploads)
            {
                Console.Write($"  [{upload.IterationName}] field '{upload.FieldName}' (was '{upload.OriginalFileName}'): ");
                string? input;
                try { input = Console.ReadLine(); }
                catch { input = null; }

                if (!string.IsNullOrWhiteSpace(input))
                    upload.SetPath(input.Trim());
            }
        }

        private void ReportFileUploads(IReadOnlyList<PendingFileUpload> uploads)
        {
            _logger.Log(_runtimeOperationIdProvider.OperationId,
                $"{uploads.Count} file upload(s) need a real path before running (edit the plan's 'path', or re-run with --prompt-files):",
                LPSLoggingLevel.Warning);
            foreach (var upload in uploads)
            {
                _logger.Log(_runtimeOperationIdProvider.OperationId,
                    $"  - iteration '{upload.IterationName}', field '{upload.FieldName}' (was '{upload.OriginalFileName}')",
                    LPSLoggingLevel.Warning);
            }
        }

        private static void TryDelete(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); }
            catch { /* best-effort cleanup */ }
        }
    }
}
