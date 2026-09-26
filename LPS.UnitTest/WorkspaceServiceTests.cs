using FluentValidation;
using LPS.UI.Common.DTOs;
using LPS.UI.Core.Web;
using LPS.UI.Core.Web.Models;
using LPS.UI.Core.Web.Services;
using Microsoft.Extensions.Logging.Abstractions;
using System.Text.Json;
using Apis.Middleware;
using Microsoft.AspNetCore.Http;
using FluentValidation.Results;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using LPS.UI.Core.Web.Controllers;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;
using Moq;
using Microsoft.Extensions.Configuration;

namespace LPS.UnitTest;

public class WorkspaceServiceTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"lps-workspace-tests-{Guid.NewGuid():N}");

    private WorkspaceOptions Options => new(_directory, Path.Combine(_directory, "missing-runner.dll"));

    [Fact]
    public void BundledDashboard_ContainsWorkspaceEntryPointAndReferencedAssets()
    {
        var webRoot = Path.Combine(AppContext.BaseDirectory, "wwwroot");
        var index = File.ReadAllText(Path.Combine(webRoot, "index.html"));
        using var assets = JsonDocument.Parse(File.ReadAllText(Path.Combine(webRoot, "asset-manifest.json")));
        var files = assets.RootElement.GetProperty("files");
        Assert.Equal("/index.html", files.GetProperty("index.html").GetString());
        Assert.Contains(files.GetProperty("main.js").GetString()!, index);
        Assert.Contains(files.GetProperty("main.css").GetString()!, index);
        foreach (var asset in files.EnumerateObject())
        {
            var relativePath = asset.Value.GetString()!.TrimStart('/');
            Assert.True(File.Exists(Path.Combine(webRoot, relativePath)), $"Missing bundled asset: {relativePath}");
        }
        using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(webRoot, "manifest.json")));
        foreach (var icon in manifest.RootElement.GetProperty("icons").EnumerateArray())
        {
            var relativePath = icon.GetProperty("src").GetString()!.TrimStart('/');
            Assert.True(File.Exists(Path.Combine(webRoot, relativePath)), $"Missing bundled icon: {relativePath}");
        }
        Assert.True(File.Exists(Path.Combine(webRoot, "lps-logo.svg")));
    }

    [Fact]
    public void BackgroundLaunch_PreservesArgumentsAndIsolatesConsoleStreams()
    {
        var args = new[] { "--port", "8017", "--open-browser", "true", "--data-directory", _directory, "--webroot", "folder with spaces" };
        var configuration = new ConfigurationBuilder().AddCommandLine(args).Build();
        var start = WebUiCommand.CreateStartInfo(args, configuration, Options, 8017);
        Assert.False(start.UseShellExecute);
        Assert.True(start.CreateNoWindow);
        Assert.True(start.RedirectStandardInput);
        Assert.True(start.RedirectStandardOutput);
        Assert.True(start.RedirectStandardError);
        Assert.Equal(Options.RunnerAssemblyPath, start.ArgumentList[0]);
        Assert.Equal("ui", start.ArgumentList[1]);
        var child = new ConfigurationBuilder().AddCommandLine(start.ArgumentList.Skip(2).ToArray()).Build();
        Assert.True(child.GetValue<bool>("background-host"));
        Assert.False(child.GetValue<bool>("open-browser"));
        Assert.Equal(8017, WebUiHost.GetPort(child));
        Assert.Equal(_directory, WebUiHost.GetOptions(child).DataDirectory);
        Assert.Equal(Path.GetFullPath("folder with spaces"), child["webroot"]);
    }

    [Theory]
    [InlineData("1023")]
    [InlineData("65536")]
    public void UiHost_RejectsInvalidPorts(string port)
    {
        var configuration = new ConfigurationBuilder().AddCommandLine(new[] { "--port", port }).Build();
        Assert.Throws<ArgumentOutOfRangeException>(() => WebUiHost.GetPort(configuration));
    }

    [Fact]
    public async Task Host_StopWaitsForResponseAndUsesTheCurrentInstanceIdentity()
    {
        var lifetime = new Mock<IHostApplicationLifetime>();
        Func<object, Task>? completed = null;
        object? completionState = null;
        var response = new Mock<IHttpResponseFeature>();
        response.Setup(value => value.OnCompleted(It.IsAny<Func<object, Task>>(), It.IsAny<object>()))
            .Callback<Func<object, Task>, object>((callback, state) => { completed = callback; completionState = state; });
        var context = new DefaultHttpContext();
        context.Features.Set(response.Object);
        var controller = new WorkspaceHostController(Options, lifetime.Object) { ControllerContext = new ControllerContext { HttpContext = context } };
        var status = Assert.IsType<WorkspaceHostStatus>(Assert.IsType<OkObjectResult>(controller.Get().Result).Value);
        Assert.Equal(WorkspaceHostStatus.ApplicationName, status.Application);
        Assert.Equal(Environment.ProcessId, status.ProcessId);
        Assert.Equal(Path.GetFullPath(_directory), status.DataDirectory);
        Assert.False(status.IsStopping);

        Assert.IsType<ConflictObjectResult>(controller.Stop(Guid.NewGuid()));
        Assert.Null(completed);
        Assert.IsType<AcceptedResult>(controller.Stop(status.InstanceId));
        lifetime.Verify(value => value.StopApplication(), Times.Never);
        await completed!(completionState!);
        lifetime.Verify(value => value.StopApplication(), Times.Once);
    }

    [Fact]
    public async Task ValidationErrors_RetainFieldPathsInProblemResponse()
    {
        var handlerType = typeof(WebUiHost).Assembly.GetType("LPS.UI.Core.Web.Middleware.WorkspaceExceptionHandler", throwOnError: true)!;
        var handler = (IExceptionHandler)Activator.CreateInstance(handlerType, nonPublic: true)!;
        using var services = new ServiceCollection().AddOptions().BuildServiceProvider();
        using var body = new MemoryStream();
        var context = new DefaultHttpContext { RequestServices = services };
        context.Response.Body = body;
        const string field = "Rounds[0].Iterations[0].TerminationRules";
        var exception = new ValidationException(new[] { new ValidationFailure(field, "Invalid grace period.") });

        Assert.True(await handler.TryHandleAsync(context, exception, CancellationToken.None));

        Assert.Equal(400, context.Response.StatusCode);
        Assert.StartsWith("application/problem+json", context.Response.ContentType);
        body.Position = 0;
        using var problem = await JsonDocument.ParseAsync(body);
        Assert.Equal("Invalid grace period.", problem.RootElement.GetProperty("errors").GetProperty(field)[0].GetString());
    }

    [Theory]
    [InlineData("127.0.0.1:8010", null, "1", "/api/workspace/plans", 200)]
    [InlineData("localhost:8010", "http://127.0.0.1:8010", "1", "/api/workspace/plans", 200)]
    [InlineData("127.0.0.1:8010", null, null, "/api/workspace/plans", 403)]
    [InlineData("example.invalid", null, "1", "/api/workspace/plans", 403)]
    [InlineData("127.0.0.1:8010", "https://example.invalid", "1", "/api/workspace/plans", 403)]
    [InlineData("127.0.0.1:8010", "null", "1", "/api/workspace/plans", 403)]
    [InlineData("127.0.0.1:8010", "https://example.invalid", null, "/hubs/metrics", 403)]
    [InlineData("127.0.0.1:8010", "http://localhost:8010", null, "/hubs/metrics", 200)]
    [InlineData("127.0.0.1:8010", null, "1", "/api/workspace/host/stop", 200)]
    [InlineData("127.0.0.1:8010", null, null, "/api/workspace/host/stop", 403)]
    [InlineData("127.0.0.1:8010", "https://example.invalid", "1", "/api/workspace/host/stop", 403)]
    public async Task Access_RequiresLocalHostAndOriginAndProtectsWorkspaceApi(string host, string? origin, string? header, string path, int expectedStatus)
    {
        var context = new DefaultHttpContext();
        context.Request.Host = new HostString(host);
        context.Request.Path = path;
        if (origin != null) context.Request.Headers.Origin = origin;
        if (header != null) context.Request.Headers["X-LPS-Workspace"] = header;
        var reachedEndpoint = false;
        var middleware = new LocalWorkspaceAccessMiddleware(_ => { reachedEndpoint = true; return Task.CompletedTask; });

        await middleware.InvokeAsync(context);

        Assert.Equal(expectedStatus, context.Response.StatusCode);
        Assert.Equal(expectedStatus == 200, reachedEndpoint);
    }

    [Fact]
    public async Task Plans_RejectMissingRequestAndEmptyRound()
    {
        var service = new WorkspacePlanService(Options);
        var invalid = CreatePlan();
        invalid.Rounds[0].Iterations[0].HttpRequest = null!;
        await Assert.ThrowsAsync<ValidationException>(() => service.SaveAsync(null, invalid, CancellationToken.None));
        invalid.Rounds[0].Iterations.Clear();
        await Assert.ThrowsAsync<ValidationException>(() => service.SaveAsync(null, invalid, CancellationToken.None));
        invalid.Rounds = null!;
        await Assert.ThrowsAsync<ValidationException>(() => service.SaveAsync(null, invalid, CancellationToken.None));
        Assert.Empty(await service.ListAsync(CancellationToken.None));
    }

    [Fact]
    public async Task Plans_PersistAcrossServiceInstancesAndSupportUpdateAndDelete()
    {
        var service = new WorkspacePlanService(Options);
        var created = await service.SaveAsync(null, CreatePlan(), CancellationToken.None);
        var restored = await new WorkspacePlanService(Options).GetAsync(created.Id, CancellationToken.None);
        Assert.Equal("Workspace test", restored.Plan.Name);
        Assert.Equal("true", restored.Plan.Rounds[0].RunClientsSequentially);
        Assert.Single(await service.ListAsync(CancellationToken.None));

        restored.Plan.Name = "Updated test";
        var updated = await service.SaveAsync(created.Id, restored.Plan, CancellationToken.None);
        Assert.Equal(created.Id, updated.Id);
        Assert.Equal("Updated test", (await service.GetAsync(created.Id, CancellationToken.None)).Plan.Name);

        await service.DeleteAsync(created.Id, CancellationToken.None);
        Assert.Empty(await service.ListAsync(CancellationToken.None));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.GetAsync(created.Id, CancellationToken.None));
    }

    [Fact]
    public async Task Plans_RejectInvalidAndEmptyPlansWithoutWritingFiles()
    {
        var service = new WorkspacePlanService(Options);
        var invalid = CreatePlan();
        invalid.Name = "../escape";
        await Assert.ThrowsAsync<ValidationException>(() => service.SaveAsync(null, invalid, CancellationToken.None));
        await Assert.ThrowsAsync<ValidationException>(() => service.SaveAsync(null,
            new PlanDto { Name = "Empty" }, CancellationToken.None));
        Assert.Empty(await service.ListAsync(CancellationToken.None));
    }

    [Fact]
    public async Task Plans_UpdateUnknownIdDoesNotCreatePlan()
    {
        var service = new WorkspacePlanService(Options);
        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.SaveAsync(Guid.NewGuid(), CreatePlan(), CancellationToken.None));
        Assert.Empty(await service.ListAsync(CancellationToken.None));
    }

    [Fact]
    public async Task Runs_ReportInterruptedAfterHostRestartAndRetainSnapshot()
    {
        var id = Guid.NewGuid();
        var runDirectory = Path.Combine(_directory, "runs", id.ToString());
        Directory.CreateDirectory(runDirectory);
        var run = new WorkspaceRun(id, Guid.NewGuid(), "Original run", "Running", DateTimeOffset.UtcNow, null, null, 8009, CreatePlan());
        await File.WriteAllTextAsync(Path.Combine(runDirectory, "run.json"), JsonSerializer.Serialize(run, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        var service = new WorkspaceRunService(new WorkspacePlanService(Options), Options, NullLogger<WorkspaceRunService>.Instance);

        var restored = await service.GetAsync(id, CancellationToken.None);

        Assert.Equal("Interrupted", restored.Run.State);
        Assert.Equal("Workspace test", restored.Run.Plan.Name);
        Assert.Single(await service.ListAsync(CancellationToken.None));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.StopAsync(id, CancellationToken.None));
    }

    [Fact]
    public async Task Runs_RejectUnavailableRunnerWithoutCreatingRun()
    {
        var plans = new WorkspacePlanService(Options);
        var saved = await plans.SaveAsync(null, CreatePlan(), CancellationToken.None);
        var runs = new WorkspaceRunService(plans, Options, NullLogger<WorkspaceRunService>.Instance);

        await Assert.ThrowsAsync<InvalidOperationException>(() => runs.StartAsync(saved.Id, CancellationToken.None));

        Assert.Empty(await runs.ListAsync(CancellationToken.None));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => runs.GetAsync(Guid.NewGuid(), CancellationToken.None));
    }

    [Fact]
    public async Task Runs_RetainCumulativeSnapshotsWithDistinctRoundNamesAndExcludeWindows()
    {
        var id = Guid.NewGuid();
        var directory = Path.Combine(_directory, "runs", id.ToString());
        Directory.CreateDirectory(directory);
        var run = new WorkspaceRun(id, Guid.NewGuid(), "Completed run", "Completed", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, 0, 8009, CreatePlan());
        await File.WriteAllTextAsync(Path.Combine(directory, "run.json"), JsonSerializer.Serialize(run, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        foreach (var round in new[] { "First", "Second" })
        {
            var metricDirectory = Path.Combine(directory, "Metrics", "Plan", round);
            Directory.CreateDirectory(metricDirectory);
            await File.WriteAllTextAsync(Path.Combine(metricDirectory, "Request_Throughput.json"),
                "[{\"IsFinal\":false,\"Metric\":{\"RequestsCount\":1}},{\"IsFinal\":true,\"Metric\":{\"RequestsCount\":10}}]");
            await File.WriteAllTextAsync(Path.Combine(metricDirectory, "Request_Windowed_Throughput.json"),
                "[{\"WindowSequence\":1,\"Metric\":{\"RequestsCount\":3}},{\"WindowSequence\":2,\"Metric\":{\"RequestsCount\":7}}]");
        }
        var service = new WorkspaceRunService(new WorkspacePlanService(Options), Options, NullLogger<WorkspaceRunService>.Instance);

        var details = await service.GetAsync(id, CancellationToken.None);

        Assert.Equal(2, details.Metrics.Count);
        Assert.Equal(2, details.Metrics.Select(metric => metric.Name).Distinct().Count());
        Assert.All(details.Metrics, metric =>
        {
            Assert.True(metric.Snapshot.GetProperty("IsFinal").GetBoolean());
            Assert.Equal(10, metric.Snapshot.GetProperty("Metric").GetProperty("RequestsCount").GetInt32());
        });
    }

    [Theory]
    [InlineData("Completed")]
    [InlineData("Failed")]
    [InlineData("Cancelled")]
    public async Task Runs_RetainCumulativeHostSnapshotsAcrossReloads(string state)
    {
        var id = Guid.NewGuid();
        var directory = Path.Combine(_directory, "runs", id.ToString());
        var metricDirectory = Path.Combine(directory, "Metrics", "Plan");
        Directory.CreateDirectory(metricDirectory);
        var run = new WorkspaceRun(id, Guid.NewGuid(), "Finished run", state, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, 0, 8009, CreatePlan());
        await File.WriteAllTextAsync(Path.Combine(directory, "run.json"), JsonSerializer.Serialize(run, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        await File.WriteAllTextAsync(Path.Combine(metricDirectory, "1_HostCumulative.json"),
            "[{\"HostKey\":{\"Scheme\":\"https\",\"Host\":\"example.com\",\"Port\":443},\"ExecutionStatus\":\"Completed\",\"IsFinal\":true,\"Throughput\":{\"RequestsCount\":10},\"Duration\":{\"TotalTime\":{\"P95\":42}}}]");
        await File.WriteAllTextAsync(Path.Combine(metricDirectory, "1_Windowed_Host.json"), "[{\"WindowSequence\":1}]");

        var service = new WorkspaceRunService(new WorkspacePlanService(Options), Options, NullLogger<WorkspaceRunService>.Instance);
        var details = await service.GetAsync(id, CancellationToken.None);
        var snapshot = Assert.Single(details.Metrics).Snapshot;

        Assert.Equal(state, details.Run.State);
        Assert.Equal("example.com", snapshot.GetProperty("HostKey").GetProperty("Host").GetString());
        Assert.True(snapshot.GetProperty("IsFinal").GetBoolean());
        Assert.Equal(10, snapshot.GetProperty("Throughput").GetProperty("RequestsCount").GetInt32());
        Assert.Equal(42, snapshot.GetProperty("Duration").GetProperty("TotalTime").GetProperty("P95").GetInt32());
        var restored = await new WorkspaceRunService(new WorkspacePlanService(Options), Options, NullLogger<WorkspaceRunService>.Instance).GetAsync(id, CancellationToken.None);
        Assert.Equal(snapshot.GetRawText(), Assert.Single(restored.Metrics).Snapshot.GetRawText());
    }

    private static PlanDto CreatePlan() => new()
    {
        Name = "Workspace test",
        Rounds = new List<RoundDto>
        {
            new()
            {
                Name = "Round 1", NumberOfClients = "2", RunClientsSequentially = "true",
                Iterations = new List<HttpIterationDto>
                {
                    new()
                    {
                        Name = "Request 1", Mode = "R", RequestCount = "1",
                        HttpRequest = new HttpRequestDto { URL = "http://127.0.0.1:8098/test", HttpMethod = "GET" }
                    }
                }
            }
        }
    };

    public void Dispose()
    {
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, recursive: true);
    }
}