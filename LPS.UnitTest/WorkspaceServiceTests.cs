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
using Moq.Protected;
using System.Net;
using System.Text;
using LPS.Domain.LPSSession;
using Microsoft.Extensions.Configuration;
using LPS.Domain.Common.Interfaces;
using LPS.Infrastructure.LPSClients;
using LPS.Infrastructure.LPSClients.HeaderServices;
using LPS.Infrastructure.LPSClients.MessageServices;
using LPS.Infrastructure.Caching;
using LPS.Infrastructure.Common;
using DomainHttpRequest = LPS.Domain.HttpRequest;

namespace LPS.UnitTest;

public class WorkspaceServiceTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"lps-workspace-tests-{Guid.NewGuid():N}");

    private WorkspaceOptions Options => new(_directory, Path.Combine(_directory, "missing-runner.dll"));

    private static WorkspaceRequestService CreateRequestService(HttpClient client)
    {
        var settings = new Mock<IWorkspaceSettingsService>();
        settings.Setup(service => service.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WorkspaceSettingsResponse("settings.json", new WorkspaceSettings(), false));
        return new WorkspaceRequestService(client, settings.Object);
    }

    [Theory]
    [InlineData("GET", "1.1", false, false)]
    [InlineData("GET", "1.1", false, true)]
    [InlineData("HEAD", "1.1", false, true)]
    [InlineData("DELETE", "1.1", false, true)]
    [InlineData("OPTIONS", "1.1", false, true)]
    [InlineData("POST", "1.1", false, false)]
    [InlineData("POST", "1.1", false, true)]
    [InlineData("PUT", "1.1", false, true)]
    [InlineData("PATCH", "1.1", false, true)]
    [InlineData("POST", "2.0", false, true)]
    [InlineData("POST", "2.0", true, true)]
    [InlineData("GET", "2.0", true, true)]
    public async Task Request_ConstructionMatchesEngine(string method, string version, bool supportH2C, bool contentHeader)
    {
        const string raw = "{\"message\":\"caf\u00e9\"}";
        var baseUrl = supportH2C ? "http://example.test" : "https://example.test";
        var resolver = new Mock<IPlaceholderResolverService>();
        resolver.Setup(service => service.ResolvePlaceholdersAsync<string>(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string input, string sessionId, CancellationToken token) => input?.Replace("$path", "resource").Replace("$body", raw).Replace("$header", "draft")!);
        resolver.Setup(service => service.ResolvePlaceholdersAsync<bool>(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string input, string sessionId, CancellationToken token) => bool.TryParse(input, out var value) && value);
        var request = new HttpRequestDto
        {
            URL = $"{baseUrl}/$path", HttpMethod = method, HttpVersion = version, SupportH2C = supportH2C.ToString(),
            Payload = new() { Type = Payload.PayloadType.Raw, Raw = "$body" },
            HttpHeaders = new() { ["X-Draft"] = "$header", ["Referrer"] = "https://example.test/source" }
        };
        if (contentHeader) request.HttpHeaders["Content-Type"] = "application/json";
        var mapper = new global::AutoMapper.MapperConfiguration(configuration => configuration.AddProfile(
            new global::LPS.AutoMapper.DtoToCommandProfile(resolver.Object, "session")), NullLoggerFactory.Instance).CreateMapper();
        var logger = Mock.Of<LPS.Domain.Common.Interfaces.ILogger>();
        var operationId = Mock.Of<IRuntimeOperationIdProvider>();
        var entity = new DomainHttpRequest(mapper.Map<DomainHttpRequest.SetupCommand>(request), logger, operationId);
        Assert.True(entity.IsValid);
        var headers = new HttpHeadersService(HttpClientConfiguration.GetDefaultInstance(), resolver.Object);
        var messages = new MessageService(headers, logger, operationId, Mock.Of<ICacheService<long>>(), Mock.Of<ICacheService<object>>(), resolver.Object);
        using var engineMessage = (await messages.BuildAsync(entity, "session")).HttpRequestMessage;
        request.URL = $"{baseUrl}/resource";
        request.Payload.Raw = raw;
        request.HttpHeaders["X-Draft"] = "draft";
        var handler = new Mock<HttpMessageHandler>();
        handler.Protected().Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .Returns<HttpRequestMessage, CancellationToken>(async (message, token) =>
            {
                Assert.Equal(engineMessage.RequestUri, message.RequestUri);
                Assert.Equal(engineMessage.Method, message.Method);
                Assert.Equal(engineMessage.Version, message.Version);
                Assert.Equal(engineMessage.VersionPolicy, message.VersionPolicy);
                var engineBody = engineMessage.Content == null ? null : await engineMessage.Content.ReadAsByteArrayAsync(token);
                var previewBody = message.Content == null ? null : await message.Content.ReadAsByteArrayAsync(token);
                Assert.Equal(engineBody, previewBody);
                Assert.Equal(engineMessage.Headers.ToString(), message.Headers.ToString());
                Assert.Equal(engineMessage.Content?.Headers.ToString(), message.Content?.Headers.ToString());
                Assert.Equal(new Uri("https://example.test/source"), message.Headers.Referrer);
                if (method is "POST" or "PUT" or "PATCH") Assert.Equal(Encoding.UTF8.GetBytes(raw), previewBody);
                return new HttpResponseMessage(HttpStatusCode.OK);
            });
        using var client = new HttpClient(handler.Object);
        await CreateRequestService(client).SendAsync(request, CancellationToken.None);
        handler.Protected().Verify("SendAsync", Times.Once(), ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>());
    }

    [Fact]
    public async Task Request_UsesCurrentHeaderSettingsBeforeSending()
    {
        var current = new WorkspaceSettings { HttpClient = new() { HeaderValidationMode = HeaderValidationMode.Strict } };
        var settings = new Mock<IWorkspaceSettingsService>();
        settings.Setup(service => service.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new WorkspaceSettingsResponse("settings.json", current, false));
        var handler = new Mock<HttpMessageHandler>(MockBehavior.Strict);
        handler.Protected().Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(() => new HttpResponseMessage(HttpStatusCode.OK));
        using var client = new HttpClient(handler.Object, disposeHandler: false);
        var service = new WorkspaceRequestService(client, settings.Object);
        var request = new HttpRequestDto { URL = "https://example.test", HttpMethod = "GET", HttpHeaders = new() { ["X-Draft"] = "yes" } };
        await Assert.ThrowsAsync<ArgumentException>(() => service.SendAsync(request, CancellationToken.None));
        handler.Protected().Verify("SendAsync", Times.Never(), ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>());
        current.HttpClient.HeaderValidationMode = HeaderValidationMode.Lenient;
        Assert.Equal(200, (await service.SendAsync(request, CancellationToken.None)).StatusCode);
        handler.Protected().Verify("SendAsync", Times.Once(), ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>());
        settings.Verify(value => value.GetAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Theory]
    [InlineData(HeaderValidationMode.Strict, false, "Accept", "application/json", true)]
    [InlineData(HeaderValidationMode.Strict, false, "Referrer", "https://example.test/source", true)]
    [InlineData(HeaderValidationMode.Strict, false, "Content-Type", "application/json", true)]
    [InlineData(HeaderValidationMode.Strict, false, "X-Draft", "yes", false)]
    [InlineData(HeaderValidationMode.Strict, false, "Connection", "close", false)]
    [InlineData(HeaderValidationMode.Lenient, false, "X-Draft", "yes", true)]
    [InlineData(HeaderValidationMode.Lenient, false, "Host", "example.test", false)]
    [InlineData(HeaderValidationMode.Lenient, true, "Host", "example.test", true)]
    [InlineData(HeaderValidationMode.RawPassthrough, false, "X-Draft", "yes\r\nInjected: value", false)]
    [InlineData(HeaderValidationMode.RawPassthrough, false, "X-Draft", "", false)]
    public async Task Request_HeaderPolicyMatchesEngine(HeaderValidationMode mode, bool allowHostOverride, string name, string value, bool accepted)
    {
        var resolver = new Mock<IPlaceholderResolverService>();
        resolver.Setup(service => service.ResolvePlaceholdersAsync<string>(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string input, string sessionId, CancellationToken token) => input);
        var configuration = new HttpClientConfiguration(TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1), 1, TimeSpan.FromSeconds(30), mode, allowHostOverride);
        var headers = new HttpHeadersService(configuration, resolver.Object);
        using var engineMessage = new HttpRequestMessage(HttpMethod.Post, "https://example.test");
        using var previewMessage = new HttpRequestMessage(HttpMethod.Post, "https://example.test");
        var engineFailure = await Record.ExceptionAsync(() => headers.ApplyHeadersAsync(engineMessage, "session", new() { [name] = value }, CancellationToken.None));
        var previewFailure = Record.Exception(() => HttpHeadersService.ApplyHeader(previewMessage, name, value, mode, allowHostOverride));
        Assert.Equal(accepted, engineFailure == null);
        Assert.Equal(engineFailure?.GetType(), previewFailure?.GetType());
        Assert.Equal(engineMessage.ToString(), previewMessage.ToString());
    }

    [Theory]
    [InlineData("GET", false)]
    [InlineData("HEAD", false)]
    [InlineData("DELETE", false)]
    [InlineData("OPTIONS", false)]
    [InlineData("POST", true)]
    [InlineData("PUT", true)]
    [InlineData("PATCH", true)]
    public async Task Request_SendsOnceWithDraftFieldsAndReturnsHttpErrors(string method, bool sendsBody)
    {
        var request = new HttpRequestDto
        {
            URL = "https://example.test/check?draft=1", HttpMethod = method, HttpVersion = "2.0",
            HttpHeaders = new() { ["Accept"] = "application/json", ["X-Draft"] = "yes", ["Content-Type"] = "application/json" },
            Payload = new() { Type = Payload.PayloadType.Raw, Raw = "{\"message\":\"draft\"}" }
        };
        var handler = new Mock<HttpMessageHandler>();
        handler.Protected().Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .Returns<HttpRequestMessage, CancellationToken>(async (message, token) =>
            {
                Assert.Equal(method, message.Method.Method);
                Assert.Equal(request.URL, message.RequestUri!.AbsoluteUri);
                Assert.Equal(HttpVersion.Version20, message.Version);
                Assert.Equal("yes", message.Headers.GetValues("X-Draft").Single());
                if (sendsBody)
                {
                    Assert.Equal(request.Payload.Raw, await message.Content!.ReadAsStringAsync(token));
                }
                else Assert.Equal("", await message.Content!.ReadAsStringAsync(token));
                Assert.Equal("application/json", message.Content!.Headers.ContentType!.MediaType);
                var response = new HttpResponseMessage(HttpStatusCode.UnprocessableEntity)
                {
                    Content = new StringContent("{\"error\":\"draft response\"}", Encoding.UTF8, "application/json")
                };
                response.Headers.Add("X-Reply", "received");
                return response;
            });
        using var client = new HttpClient(handler.Object);
        var result = await CreateRequestService(client).SendAsync(request, CancellationToken.None);
        Assert.Equal(422, result.StatusCode);
        Assert.Equal("{\"error\":\"draft response\"}", result.Body);
        Assert.Equal(new[] { "received" }, result.Headers["X-Reply"]);
        Assert.Contains("application/json", result.Headers["Content-Type"][0]);
        Assert.Equal("text", result.BodyEncoding);
        Assert.False(result.Truncated);
        Assert.True(result.ElapsedMilliseconds >= 0);
        Assert.Equal("{\"message\":\"draft\"}", request.Payload.Raw);
        Assert.False(Directory.Exists(_directory));
        handler.Protected().Verify("SendAsync", Times.Once(), ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>());
    }

    [Theory]
    [InlineData("file:///example.txt", "GET", "1.1")]
    [InlineData("/relative", "GET", "1.1")]
    [InlineData("https://example.test", "TRACE", "1.1")]
    [InlineData("https://example.test", "GET", "3.0")]
    public async Task Request_RejectsInvalidInputsWithoutSending(string url, string method, string version)
    {
        var handler = new Mock<HttpMessageHandler>(MockBehavior.Strict);
        using var client = new HttpClient(handler.Object, disposeHandler: false);
        await Assert.ThrowsAsync<ArgumentException>(() => CreateRequestService(client).SendAsync(
            new HttpRequestDto { URL = url, HttpMethod = method, HttpVersion = version }, CancellationToken.None));
        handler.Protected().Verify("SendAsync", Times.Never(), ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>());
    }

    [Fact]
    public async Task Request_LimitsResponseAndEncodesBinaryContent()
    {
        var bytes = new byte[1024 * 1024 + 20];
        bytes[0] = 255;
        var handler = new Mock<HttpMessageHandler>();
        handler.Protected().Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(() =>
            {
                var content = new ByteArrayContent(bytes);
                content.Headers.ContentType = new("application/octet-stream");
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = content };
            });
        using var client = new HttpClient(handler.Object);
        var result = await CreateRequestService(client).SendAsync(new HttpRequestDto { URL = "https://example.test", HttpMethod = "GET" }, CancellationToken.None);
        Assert.True(result.Truncated);
        Assert.Equal(1024 * 1024, result.BodyBytes);
        Assert.Equal("base64", result.BodyEncoding);
        Assert.Equal(bytes.Take(1024 * 1024), Convert.FromBase64String(result.Body));
    }

    [Fact]
    public async Task Request_TimeoutIncludesReadingTheResponseBody()
    {
        var stream = new Mock<Stream>();
        stream.SetupGet(value => value.CanRead).Returns(true);
        stream.Setup(value => value.ReadAsync(It.IsAny<Memory<byte>>(), It.IsAny<CancellationToken>()))
            .Returns<Memory<byte>, CancellationToken>(async (buffer, token) =>
            {
                await Task.Delay(Timeout.Infinite, token);
                return 0;
            });
        var handler = new Mock<HttpMessageHandler>();
        handler.Protected().Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(() => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(stream.Object) });
        using var client = new HttpClient(handler.Object) { Timeout = TimeSpan.FromMilliseconds(100) };
        await Assert.ThrowsAsync<TimeoutException>(() => CreateRequestService(client).SendAsync(
            new HttpRequestDto { URL = "https://example.test", HttpMethod = "GET" }, CancellationToken.None));
    }

    [Fact]
    public async Task Request_PropagatesCallerCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        var handler = new Mock<HttpMessageHandler>();
        handler.Protected().Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .Returns<HttpRequestMessage, CancellationToken>(async (message, token) =>
            {
                cancellation.Cancel();
                await Task.Delay(Timeout.Infinite, token);
                return new HttpResponseMessage(HttpStatusCode.OK);
            });
        using var client = new HttpClient(handler.Object);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => CreateRequestService(client).SendAsync(
            new HttpRequestDto { URL = "https://example.test", HttpMethod = "GET" }, cancellation.Token));
    }

    private async Task<WorkspaceSettingsService> CreateSettingsServiceAsync()
    {
        Directory.CreateDirectory(_directory);
        var path = Path.Combine(_directory, "lpsSettings.json");
        await File.WriteAllTextAsync(path, """
            { "OtherRoot": { "Keep": true }, "LPSAppSettings": {
              "FileLogger": { "LogFilePath": "logs/lpslog.log", "ConsoleLogingLevel": 1, "EnableConsoleLogging": true, "DisableConsoleErrorLogging": true, "DisableFileLogging": false, "LoggingLevel": 0 },
              "Watchdog": { "MaxMemoryMB": 5000, "CoolDownMemoryMB": 4000, "MaxCPUPercentage": 80, "CoolDownCPUPercentage": 60, "CoolDownRetryTimeInMs": 100, "MaxConcurrentConnectionsCountPerHostName": 3000, "CoolDownConcurrentConnectionsCountPerHostName": 1500, "MaxCoolingPeriod": 30, "ResumeCoolingAfter": 90, "SuspensionMode": 0, "FutureOption": 42 },
              "HttpClient": { "ClientTimeoutInSeconds": 60, "PooledConnectionLifeTimeInSeconds": 1500, "PooledConnectionIdleTimeoutInSeconds": 350, "MaxConnectionsPerServer": 3000, "HeaderValidationMode": "RawPassthrough", "AllowHostOverride": false },
              "Dashboard": { "BuiltInDashboard": true, "Port": 8009, "RefreshRate": 3 },
              "InfluxDB": { "Enabled": false, "Url": "https://influx.example", "Token": "test-token", "Organization": "LPS", "Bucket": "LPS_Metrics" },
              "Cluster": { "GRPCPort": 5001 }
            } }
            """);
        return new WorkspaceSettingsService(path);
    }

    [Fact]
    public async Task Settings_SavePreservesUnknownFieldsAndTokenAndFeedsFutureRuns()
    {
        var service = await CreateSettingsServiceAsync();
        var loaded = await service.GetAsync(CancellationToken.None);
        Assert.True(loaded.HasInfluxDBToken);
        Assert.Null(loaded.Settings.InfluxDB.Token);
        Assert.Equal(250, loaded.Settings.LiveMetrics.PublishIntervalMs);
        loaded.Settings.Watchdog.MaxMemoryMB = 6000;
        loaded.Settings.InfluxDB.Enabled = true;

        var saved = await service.SaveAsync(loaded.Settings, CancellationToken.None);

        Assert.Null(saved.Settings.InfluxDB.Token);
        Assert.True(saved.HasInfluxDBToken);
        using var document = JsonDocument.Parse(await File.ReadAllTextAsync(loaded.FilePath));
        var app = document.RootElement.GetProperty("LPSAppSettings");
        Assert.True(document.RootElement.GetProperty("OtherRoot").GetProperty("Keep").GetBoolean());
        Assert.Equal(42, app.GetProperty("Watchdog").GetProperty("FutureOption").GetInt32());
        Assert.Equal(5001, app.GetProperty("Cluster").GetProperty("GRPCPort").GetInt32());
        Assert.Equal("test-token", app.GetProperty("InfluxDB").GetProperty("Token").GetString());
        Assert.True(app.GetProperty("FileLogger").TryGetProperty("ConsoleLogingLevel", out _));
        Assert.Equal(6000, (await new WorkspaceSettingsService(loaded.FilePath).GetAsync(CancellationToken.None)).Settings.Watchdog.MaxMemoryMB);
        var copied = await WorkspaceRunService.WriteSettingsAsync(_directory, 9010, 9011, CancellationToken.None, loaded.FilePath);
        using var run = JsonDocument.Parse(await File.ReadAllTextAsync(copied));
        Assert.Equal(6000, run.RootElement.GetProperty("LPSAppSettings").GetProperty("Watchdog").GetProperty("MaxMemoryMB").GetInt32());
        Assert.True(run.RootElement.GetProperty("LPSAppSettings").GetProperty("InfluxDB").GetProperty("Enabled").GetBoolean());
        Assert.Empty(Directory.GetFiles(_directory, "*.tmp"));
    }

    [Theory]
    [InlineData("Watchdog.MaxMemoryMB")]
    [InlineData("HttpClient")]
    [InlineData("Dashboard.Port")]
    [InlineData("LiveMetrics.PublishIntervalMs")]
    [InlineData("InfluxDB.Token")]
    [InlineData("FileLogger")]
    public async Task Settings_RejectInvalidValuesWithoutWriting(string field)
    {
        var service = await CreateSettingsServiceAsync();
        var loaded = await service.GetAsync(CancellationToken.None);
        var original = await File.ReadAllTextAsync(loaded.FilePath);
        var settings = loaded.Settings;
        switch (field)
        {
            case "Watchdog.MaxMemoryMB": settings.Watchdog.MaxMemoryMB = 2000; break;
            case "HttpClient": settings.HttpClient.PooledConnectionIdleTimeoutInSeconds = 1600; break;
            case "Dashboard.Port": settings.Dashboard.Port = 70000; break;
            case "LiveMetrics.PublishIntervalMs": settings.LiveMetrics.PublishIntervalMs = 0; break;
            case "InfluxDB.Token": settings.InfluxDB.Enabled = true; settings.InfluxDB.Token = ""; break;
            case "FileLogger": settings = settings with { FileLogger = null! }; break;
        }

        var failure = await Assert.ThrowsAsync<ValidationException>(() => service.SaveAsync(settings, CancellationToken.None));

        Assert.Contains(failure.Errors, error => error.PropertyName == field);
        Assert.Equal(original, await File.ReadAllTextAsync(loaded.FilePath));
    }

    [Fact]
    public async Task Settings_TokenCanBeReplacedOrExplicitlyCleared()
    {
        var service = await CreateSettingsServiceAsync();
        var loaded = await service.GetAsync(CancellationToken.None);
        loaded.Settings.InfluxDB.Token = "replacement-test-token";
        var replaced = await service.SaveAsync(loaded.Settings, CancellationToken.None);
        Assert.True(replaced.HasInfluxDBToken);
        Assert.Null(replaced.Settings.InfluxDB.Token);
        using var document = JsonDocument.Parse(await File.ReadAllTextAsync(loaded.FilePath));
        Assert.Equal("replacement-test-token", document.RootElement.GetProperty("LPSAppSettings").GetProperty("InfluxDB").GetProperty("Token").GetString());

        replaced.Settings.InfluxDB.Token = "";
        var cleared = await service.SaveAsync(replaced.Settings, CancellationToken.None);
        Assert.False(cleared.HasInfluxDBToken);
        Assert.False((await service.GetAsync(CancellationToken.None)).HasInfluxDBToken);
    }

    [Fact]
    public async Task Runs_CopyConfiguredSettingsWithoutChangingTheSource()
    {
        Directory.CreateDirectory(_directory);
        var source = Path.Combine(_directory, "lpsSettings.json");
        var original = """
            { "LPSAppSettings": {
              "Watchdog": { "MaxMemoryMB": 5000 },
              "Dashboard": { "BuiltInDashboard": true, "Port": 8009, "RefreshRate": 7 },
              "LiveMetrics": { "PublishIntervalMs": 250 },
              "InfluxDB": { "Enabled": true, "Url": "https://influx.example", "Token": "test-token" }
            } }
            """;
        await File.WriteAllTextAsync(source, original);
        var runDirectory = Path.Combine(_directory, "run");
        Directory.CreateDirectory(runDirectory);

        var copied = await WorkspaceRunService.WriteSettingsAsync(runDirectory, 9010, 9011, CancellationToken.None, source);

        using var document = JsonDocument.Parse(await File.ReadAllTextAsync(copied));
        var settings = document.RootElement.GetProperty("LPSAppSettings");
        Assert.Equal(7, settings.GetProperty("Dashboard").GetProperty("RefreshRate").GetInt32());
        Assert.Equal(9010, settings.GetProperty("Dashboard").GetProperty("Port").GetInt32());
        Assert.False(settings.GetProperty("Dashboard").GetProperty("BuiltInDashboard").GetBoolean());
        Assert.Equal(9011, settings.GetProperty("Cluster").GetProperty("MasterNodePort").GetInt32());
        Assert.False(settings.GetProperty("Cluster").TryGetProperty("GRPCPort", out _));
        Assert.True(settings.GetProperty("InfluxDB").GetProperty("Enabled").GetBoolean());
        Assert.Equal("test-token", settings.GetProperty("InfluxDB").GetProperty("Token").GetString());
        Assert.Equal(5000, settings.GetProperty("Watchdog").GetProperty("MaxMemoryMB").GetInt32());
        Assert.Equal(250, settings.GetProperty("LiveMetrics").GetProperty("PublishIntervalMs").GetInt32());
        Assert.Equal(original, await File.ReadAllTextAsync(source));
    }

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

    [Theory]
    [InlineData(null, "json")]
    [InlineData("json", "json")]
    [InlineData("yaml", "yaml")]
    [InlineData("YAML", "yaml")]
    public async Task Plans_ExportFormatsPreservePlanAndStorage(string? requestedFormat, string expectedFormat)
    {
        var service = new WorkspacePlanService(Options);
        var plan = CreatePlan();
        plan.Rounds[0].Iterations[0].HttpRequest.HttpMethod = "POST";
        plan.Rounds[0].Iterations[0].HttpRequest.Payload = new() { Type = Payload.PayloadType.Raw, Raw = "{\"message\":\"caf\u00e9\"}" };
        var saved = await service.SaveAsync(null, plan, CancellationToken.None);
        var path = Path.Combine(_directory, "plans", $"{saved.Id}.json");
        var original = await File.ReadAllBytesAsync(path);
        var controller = new WorkspacePlansController(service);

        var result = Assert.IsType<FileContentResult>(requestedFormat == null
            ? await controller.Export(saved.Id, CancellationToken.None)
            : await controller.Export(saved.Id, CancellationToken.None, requestedFormat));

        Assert.Equal($"application/{expectedFormat}", result.ContentType);
        Assert.Equal($"{plan.Name}.{expectedFormat}", result.FileDownloadName);
        var content = Encoding.UTF8.GetString(result.FileContents);
        var restored = expectedFormat == "yaml"
            ? SerializationHelper.DeserializeFromYaml<PlanDto>(content)
            : SerializationHelper.Deserialize<PlanDto>(content);
        Assert.Equal(SerializationHelper.Serialize(plan), SerializationHelper.Serialize(restored));
        Assert.Equal(original, await File.ReadAllBytesAsync(path));
        Assert.Single(await service.ListAsync(CancellationToken.None));
    }

    [Theory]
    [InlineData("xml")]
    [InlineData("csv")]
    public async Task Plans_ExportRejectsUnsupportedFormats(string format)
    {
        var service = new Mock<IWorkspacePlanService>(MockBehavior.Strict);
        var controller = new WorkspacePlansController(service.Object);

        var result = Assert.IsType<ObjectResult>(await controller.Export(Guid.NewGuid(), CancellationToken.None, format));

        Assert.Equal(400, result.StatusCode);
        Assert.Equal("Use json or yaml.", Assert.IsType<ProblemDetails>(result.Value).Detail);
        service.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Plans_ExportUnknownIdPreservesNotFoundBehavior()
    {
        var service = new WorkspacePlanService(Options);
        var controller = new WorkspacePlansController(service);
        await Assert.ThrowsAsync<KeyNotFoundException>(() => controller.Export(Guid.NewGuid(), CancellationToken.None, "yaml"));
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