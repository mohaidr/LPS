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
using LPS.Infrastructure.Common;
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

                    var output = parse.GetValueForArgument(LPSRecordCommandOptions.OutputFileArgument) ?? "RecordedPlan.yaml";
                    var explicitName = parse.GetValueForOption(LPSRecordCommandOptions.PlanNameOption);
                    var planName = !string.IsNullOrWhiteSpace(explicitName) ? explicitName! : DerivePlanName(output);
                    var startUrl = parse.GetValueForOption(LPSRecordCommandOptions.UrlOption);
                    var headless = parse.GetValueForOption(LPSRecordCommandOptions.HeadlessOption);
                    var ignoreContentTypes = parse.GetValueForOption(LPSRecordCommandOptions.IgnoreContentTypeOption);
                    var ignoreExtensions = parse.GetValueForOption(LPSRecordCommandOptions.IgnoreExtensionOption);
                    var ignoreMethods = parse.GetValueForOption(LPSRecordCommandOptions.IgnoreMethodOption);
                    var ignoreResourceTypes = parse.GetValueForOption(LPSRecordCommandOptions.IgnoreResourceTypeOption);
                    var onlyHosts = parse.GetValueForOption(LPSRecordCommandOptions.OnlyHostOption);
                    var ignoreHosts = parse.GetValueForOption(LPSRecordCommandOptions.IgnoreHostOption);
                    var ignorePaths = parse.GetValueForOption(LPSRecordCommandOptions.IgnorePathOption);
                    var ignoreHeaders = parse.GetValueForOption(LPSRecordCommandOptions.IgnoreHeaderOption);
                    var minimalHeaders = parse.GetValueForOption(LPSRecordCommandOptions.MinimalHeadersOption);

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
                    var append = parse.GetValueForOption(LPSRecordCommandOptions.AppendOption);
                    var update = parse.GetValueForOption(LPSRecordCommandOptions.UpdateOption);
                    var roundNameOption = parse.GetValueForOption(LPSRecordCommandOptions.RoundNameOption);

                    // --update upserts by request identity; --append always adds. If both are given, --update wins.
                    if (append && update)
                    {
                        _logger.Log(_runtimeOperationIdProvider.OperationId,
                            "Both --append and --update were specified; using --update.", LPSLoggingLevel.Warning);
                        append = false;
                    }

                    // --append and --update both merge into an existing file; otherwise (or if it's new/empty) we overwrite.
                    var mergeMode = (append || update) && File.Exists(output);
                    PlanDto? existingPlan = null;
                    if (mergeMode)
                    {
                        string existingText;
                        try
                        {
                            existingText = File.ReadAllText(output);
                        }
                        catch (Exception ex)
                        {
                            _logger.Log(_runtimeOperationIdProvider.OperationId,
                                $"Could not read '{output}' to merge into — aborting so it isn't overwritten. ({ex.Message})",
                                LPSLoggingLevel.Error);
                            return;
                        }

                        if (string.IsNullOrWhiteSpace(existingText))
                        {
                            // Empty/blank file: nothing to merge into, so just write a fresh plan.
                            mergeMode = false;
                        }
                        else
                        {
                            existingPlan = ParsePlan(existingText, output);
                            if (existingPlan == null)
                            {
                                _logger.Log(_runtimeOperationIdProvider.OperationId,
                                    $"Could not parse '{output}' to merge into — aborting so it isn't overwritten.",
                                    LPSLoggingLevel.Error);
                                return;
                            }
                            existingPlan.Rounds ??= new List<RoundDto>();
                        }
                    }

                    // Where new requests land. --update defaults to the first existing round; --append to a new round.
                    string roundName;
                    if (!string.IsNullOrWhiteSpace(roundNameOption))
                        roundName = roundNameOption!;
                    else if (!mergeMode)
                        roundName = "Main";
                    else if (update)
                        roundName = existingPlan!.Rounds.FirstOrDefault()?.Name ?? "Main";
                    else
                        roundName = $"Round{existingPlan!.Rounds.Count + 1}";

                    var thinkTime = parse.GetValueForOption(LPSRecordCommandOptions.ThinkTimeOption);
                    var maxThinkTimeRaw = parse.GetValueForOption(LPSRecordCommandOptions.MaxThinkTimeOption);
                    int? maxThinkTime = null;
                    if (!string.IsNullOrWhiteSpace(maxThinkTimeRaw) && int.TryParse(maxThinkTimeRaw, out var mtt) && mtt >= 0)
                        maxThinkTime = mtt;

                    var runInParallel = parse.GetValueForOption(LPSRecordCommandOptions.RunInParallelOption);
                    if (thinkTime && runInParallel)
                    {
                        _logger.Log(_runtimeOperationIdProvider.OperationId,
                            "--think-time sets a per-step startupDelay, which paces a sequential journey; with --run-in-parallel the steps start together, so it acts as a fixed offset instead.",
                            LPSLoggingLevel.Warning);
                    }

                    var shape = new RecordPlanOptions
                    {
                        RoundName = roundName,
                        NumberOfClients = parse.GetValueForOption(LPSRecordCommandOptions.NumberOfClientsOption),
                        ArrivalDelay = parse.GetValueForOption(LPSRecordCommandOptions.ArrivalDelayOption),
                        RunInParallel = runInParallel,
                        RequestCount = parse.GetValueForOption(LPSRecordCommandOptions.RequestCountOption),
                        Duration = parse.GetValueForOption(LPSRecordCommandOptions.DurationOption),
                        ThinkTime = thinkTime,
                        MaxThinkTime = maxThinkTime
                    };

                    var filter = new CaptureFilter(
                        ignoreContentTypes, ignoreExtensions, ignoreMethods, ignoreResourceTypes,
                        onlyHosts, ignoreHosts, ignorePaths, ignoreHeaders, minimalHeaders);
                    var conversion = new HarToPlanConverter().Convert(har ?? new HarRoot(), filter, planName, shape);
                    var newRound = conversion.Plan.Rounds[0];

                    var capturedCount = har?.Log?.Entries?.Count ?? 0;
                    var iterationCount = newRound.Iterations?.Count ?? 0;
                    if (iterationCount == 0)
                    {
                        _logger.Log(_runtimeOperationIdProvider.OperationId,
                            $"Captured {capturedCount} request(s) but 0 remained after filtering — nothing was written. Check your --only-host / --ignore-* filters.",
                            LPSLoggingLevel.Warning);
                        return;
                    }
                    var newIterations = newRound.Iterations!;
                    if (conversion.FileUploads.Count > 0)
                    {
                        if (promptFiles)
                            PromptForFilePaths(conversion.FileUploads);
                        else
                            ReportFileUploads(conversion.FileUploads);
                    }

                    PlanDto planToSave;
                    string summary;
                    if (!mergeMode)
                    {
                        planToSave = conversion.Plan;
                        summary = $"Recorded {iterationCount} request(s) into '{output}'.";
                    }
                    else if (update)
                    {
                        planToSave = existingPlan!;
                        var (updated, added) = UpsertIterations(planToSave, roundName, newIterations);
                        if (RoundShapeProvided(shape))
                        {
                            _logger.Log(_runtimeOperationIdProvider.OperationId,
                                "Round-level options (--number-of-clients / --arrival-delay / --run-in-parallel) were ignored in --update mode.",
                                LPSLoggingLevel.Warning);
                        }
                        summary = $"Updated {updated} and added {added} iteration(s) in '{output}'.";
                    }
                    else
                    {
                        planToSave = existingPlan!;
                        var target = planToSave.Rounds
                            .FirstOrDefault(r => string.Equals(r.Name, roundName, StringComparison.OrdinalIgnoreCase));

                        if (target != null)
                        {
                            MergeIterations(target, newIterations);
                            if (RoundShapeProvided(shape))
                            {
                                _logger.Log(_runtimeOperationIdProvider.OperationId,
                                    $"Round-level options (--number-of-clients / --arrival-delay / --run-in-parallel) were ignored — the requests were added to the existing round '{target.Name}', which keeps its own settings.",
                                    LPSLoggingLevel.Warning);
                            }
                            summary = $"Appended {iterationCount} request(s) to round '{target.Name}' in '{output}'.";
                        }
                        else
                        {
                            newRound.Name = UniqueRoundName(roundName, planToSave);
                            planToSave.Rounds.Add(newRound);
                            summary = $"Appended a new round '{newRound.Name}' with {iterationCount} request(s) to '{output}'.";
                        }
                    }

                    var validation = new PlanValidator(planToSave).Validate();
                    if (!validation.IsValid)
                    {
                        validation.PrintValidationErrors();
                    }

                    ConfigurationService.SaveConfiguration(output, planToSave);

                    _logger.Log(_runtimeOperationIdProvider.OperationId, summary, LPSLoggingLevel.Information);
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

        private static PlanDto? ParsePlan(string text, string path)
        {
            try
            {
                return path.EndsWith(".json", StringComparison.OrdinalIgnoreCase)
                    ? SerializationHelper.Deserialize<PlanDto>(text)
                    : SerializationHelper.DeserializeFromYaml<PlanDto>(text);
            }
            catch
            {
                return null;
            }
        }

        private static void MergeIterations(RoundDto target, List<HttpIterationDto> newIterations)
        {
            target.Iterations ??= new List<HttpIterationDto>();
            var used = new HashSet<string>(
                target.Iterations.Select(i => i.Name ?? string.Empty),
                StringComparer.OrdinalIgnoreCase);

            foreach (var iteration in newIterations)
            {
                iteration.Name = UniqueName(string.IsNullOrWhiteSpace(iteration.Name) ? "request" : iteration.Name, used);
                target.Iterations.Add(iteration);
            }
        }

        // --update: upsert each recorded iteration into the plan by request identity (method + URL + body).
        // A match refreshes the existing request in place (keeping its name/tuning); anything new is added to the target round.
        internal static (int updated, int added) UpsertIterations(
            PlanDto plan, string targetRoundName, List<HttpIterationDto> newIterations)
        {
            plan.Rounds ??= new List<RoundDto>();

            // Index every existing iteration in the plan by request identity.
            var index = new Dictionary<string, HttpIterationDto>(StringComparer.Ordinal);
            foreach (var round in plan.Rounds)
            {
                if (round.Iterations is null)
                    continue;
                foreach (var iteration in round.Iterations)
                    index[RequestKey(iteration)] = iteration;
            }

            var targetRound = plan.Rounds
                .FirstOrDefault(r => string.Equals(r.Name, targetRoundName, StringComparison.OrdinalIgnoreCase));
            HashSet<string>? usedNames = null;

            var updated = 0;
            var added = 0;
            foreach (var incoming in newIterations)
            {
                var key = RequestKey(incoming);
                if (index.TryGetValue(key, out var existing))
                {
                    // Same request already in the plan: refresh what we captured, keep the user's tuning/name.
                    existing.HttpRequest = incoming.HttpRequest;
                    updated++;
                }
                else
                {
                    if (targetRound is null)
                    {
                        targetRound = new RoundDto { Name = targetRoundName };
                        plan.Rounds.Add(targetRound);
                    }
                    targetRound.Iterations ??= new List<HttpIterationDto>();
                    usedNames ??= new HashSet<string>(
                        targetRound.Iterations.Select(i => i.Name ?? string.Empty),
                        StringComparer.OrdinalIgnoreCase);

                    incoming.Name = UniqueName(
                        string.IsNullOrWhiteSpace(incoming.Name) ? "request" : incoming.Name, usedNames);
                    targetRound.Iterations.Add(incoming);
                    index[key] = incoming;
                    added++;
                }
            }

            return (updated, added);
        }

        private static string RequestKey(HttpIterationDto iteration)
        {
            var request = iteration.HttpRequest;
            var method = (request?.HttpMethod ?? string.Empty).ToUpperInvariant();
            var url = request?.URL ?? string.Empty;
            return $"{method} {url} {PayloadSignature(request)}";
        }

        // A lightweight body fingerprint so two calls to the same URL with different bodies
        // (e.g. GraphQL/RPC) are treated as different requests, not duplicates.
        private static string PayloadSignature(HttpRequestDto? request)
        {
            var payload = request?.Payload;
            if (payload is null)
                return string.Empty;

            if (!string.IsNullOrEmpty(payload.Raw))
                return "raw:" + payload.Raw;

            if (!string.IsNullOrEmpty(payload.File))
                return "file:" + payload.File;

            if (payload.Multipart is not null)
            {
                var fields = string.Join("&",
                    (payload.Multipart.Fields ?? new List<TextFieldDto>()).Select(f => $"{f.Name}={f.Value}"));
                var files = string.Join("&",
                    (payload.Multipart.Files ?? new List<FileFieldDto>()).Select(f => f.Name));
                return $"mp:{fields}|{files}";
            }

            return string.Empty;
        }

        private static string UniqueRoundName(string name, PlanDto plan)
        {
            var used = new HashSet<string>(
                (plan.Rounds ?? new List<RoundDto>()).Select(r => r.Name ?? string.Empty),
                StringComparer.OrdinalIgnoreCase);
            return UniqueName(name, used);
        }

        private static string UniqueName(string name, HashSet<string> used)
        {
            if (used.Add(name))
                return name;

            var index = 2;
            string candidate;
            do
            {
                candidate = $"{name}_{index++}";
            }
            while (!used.Add(candidate));

            return candidate;
        }

        private static bool RoundShapeProvided(RecordPlanOptions shape)
        {
            return !string.IsNullOrWhiteSpace(shape.NumberOfClients)
                || !string.IsNullOrWhiteSpace(shape.ArrivalDelay)
                || shape.RunInParallel;
        }

        private static string DerivePlanName(string outputPath)
        {
            var name = Path.GetFileNameWithoutExtension(outputPath) ?? string.Empty;

            // Plan names allow letters, digits, spaces, '_', '.', and '-'; replace anything else.
            var cleaned = new string(name
                .Select(c => char.IsLetterOrDigit(c) || c is ' ' or '_' or '.' or '-' ? c : '_')
                .ToArray())
                .Trim();

            return string.IsNullOrWhiteSpace(cleaned) ? "RecordedPlan" : cleaned;
        }

        private static void TryDelete(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); }
            catch { /* best-effort cleanup */ }
        }
    }
}
