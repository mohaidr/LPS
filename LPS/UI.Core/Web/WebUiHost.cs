using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using LPS.UI.Core.Host;
using LPS.UI.Core.Web.Middleware;
using LPS.UI.Core.Web.Services;
using Apis.Middleware;
using Microsoft.OpenApi.Models;

namespace LPS.UI.Core.Web;

internal static class WebUiHost
{
    internal static int GetPort(IConfiguration configuration)
    {
        var port = configuration.GetValue("port", 8010);
        if (port < 1024 || port > 65535)
            throw new ArgumentOutOfRangeException(nameof(port), "The UI port must be between 1024 and 65535.");
        return port;
    }

    internal static WorkspaceOptions GetOptions(IConfiguration configuration) => new(
        Path.GetFullPath(configuration["data-directory"] ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LPS", "Workspace")),
        typeof(WebUiHost).Assembly.Location);

    public static async Task RunAsync(WebApplicationBuilder builder)
    {
        var port = GetPort(builder.Configuration);
        builder.WebHost.ConfigureKestrel(options => options.Listen(IPAddress.Loopback, port));
        builder.Services.AddSingleton(GetOptions(builder.Configuration));
        builder.Services.Configure<HostOptions>(options => options.ShutdownTimeout = TimeSpan.FromSeconds(90));
        builder.Services.AddSingleton<IWorkspacePlanService, WorkspacePlanService>();
        builder.Services.AddHttpClient<IWorkspaceRequestService, WorkspaceRequestService>(client => client.Timeout = TimeSpan.FromSeconds(30))
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
            {
                AllowAutoRedirect = false,
                UseCookies = false,
                AutomaticDecompression = DecompressionMethods.All
            });
        builder.Services.AddSingleton<IWorkspaceSettingsService>(new WorkspaceSettingsService(LPS.Infrastructure.Common.AppConstants.AppSettingsFileLocation));
        builder.Services.AddSingleton<WorkspaceRunService>();
        builder.Services.AddSingleton<IWorkspaceRunService>(provider => provider.GetRequiredService<WorkspaceRunService>());
        builder.Services.AddHostedService(provider => provider.GetRequiredService<WorkspaceRunService>());
        builder.Services.AddExceptionHandler<WorkspaceExceptionHandler>();
        builder.Services.AddProblemDetails();
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new() { Title = "LPS Workspace", Version = "v1" });
            options.AddSecurityDefinition("Workspace", new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.ApiKey, In = ParameterLocation.Header,
                Name = "X-LPS-Workspace", Description = "Enter 1 for local workspace requests."
            });
            options.AddSecurityRequirement(new OpenApiSecurityRequirement
            {
                [new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Workspace" } }] = Array.Empty<string>()
            });
        });
        builder.Services.AddControllers(options => options.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true)
            .ConfigureApplicationPartManager(manager =>
        {
            manager.ApplicationParts.Clear();
            manager.ApplicationParts.Add(new AssemblyPart(typeof(WebUiHost).Assembly));
        });

        var app = builder.Build();
        app.UseExceptionHandler();
        app.UseMiddleware<LocalWorkspaceAccessMiddleware>();

        app.UseSwagger();
        app.UseSwaggerUI();
        app.MapGet("/", () => Results.Redirect("/workspace")).ExcludeFromDescription();

        var webRoot = builder.Configuration["webroot"] ?? Path.Combine(AppContext.BaseDirectory, "wwwroot");
        if (Directory.Exists(webRoot))
        {
            var files = new PhysicalFileProvider(Path.GetFullPath(webRoot));
            app.UseDefaultFiles(new DefaultFilesOptions { FileProvider = files });
            app.UseStaticFiles(new StaticFileOptions { FileProvider = files });
            app.MapFallbackToFile("workspace/{*path:nonfile}", "index.html", new StaticFileOptions { FileProvider = files });
            app.MapFallbackToFile("admin/{*path:nonfile}", "index.html", new StaticFileOptions { FileProvider = files });
        }

        app.MapControllers();
        using var browserLaunch = builder.Configuration.GetValue("open-browser", true)
            ? app.Lifetime.ApplicationStarted.Register(() => DashboardService.OpenBrowser($"http://127.0.0.1:{port}/workspace"))
            : default;
        await app.RunAsync();
    }
}