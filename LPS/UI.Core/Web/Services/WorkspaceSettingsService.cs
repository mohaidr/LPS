using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using FluentValidation;
using FluentValidation.Results;
using LPS.UI.Core.LPSValidators;
using LPS.UI.Core.Web.Models;

namespace LPS.UI.Core.Web.Services;

public sealed class WorkspaceSettingsService(string filePath) : IWorkspaceSettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() }
    };

    public async Task<WorkspaceSettingsResponse> GetAsync(CancellationToken token)
    {
        var document = await ReadAsync(token);
        var settings = document["LPSAppSettings"]!.Deserialize<WorkspaceSettings>(JsonOptions)!;
        return Response(settings);
    }

    public async Task<WorkspaceSettingsResponse> SaveAsync(WorkspaceSettings settings, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var document = await ReadAsync(token);
        var app = document["LPSAppSettings"]!.AsObject();
        if (settings.InfluxDB != null)
            settings.InfluxDB.Token ??= app["InfluxDB"]?["Token"]?.GetValue<string>();

        var failures = new List<ValidationFailure>();
        Validate(settings.FileLogger, "FileLogger", new FileLoggerValidator(), failures);
        Validate(settings.Watchdog, "Watchdog", new WatchdogValidator(), failures);
        Validate(settings.HttpClient, "HttpClient", new HttpClientValidator(), failures);
        Validate(settings.Dashboard, "Dashboard", new DashboardConfigurationValidator(), failures);
        Validate(settings.InfluxDB, "InfluxDB", new InfluxDBValidator(), failures);
        if (settings.LiveMetrics == null || settings.LiveMetrics.PublishIntervalMs <= 0)
            failures.Add(new("LiveMetrics.PublishIntervalMs", "Publish interval must be greater than 0 milliseconds."));
        if (failures.Count > 0)
            throw new ValidationException(failures);

        var updates = JsonSerializer.SerializeToNode(settings, JsonOptions)!.AsObject();
        foreach (var section in updates)
        {
            var target = app[section.Key] as JsonObject ?? new JsonObject();
            if (app[section.Key] is not JsonObject)
                app[section.Key] = target;
            foreach (var property in section.Value!.AsObject())
                target[property.Key] = property.Value?.DeepClone();
        }

        var temporary = $"{filePath}.{Guid.NewGuid():N}.tmp";
        try
        {
            await File.WriteAllTextAsync(temporary, document.ToJsonString(JsonOptions), token);
            File.Move(temporary, filePath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
        return Response(settings);
    }

    private async Task<JsonObject> ReadAsync(CancellationToken token)
    {
        var document = JsonNode.Parse(await File.ReadAllTextAsync(filePath, token)) as JsonObject;
        if (document?["LPSAppSettings"] is not JsonObject)
            throw new InvalidOperationException("The settings file must contain an LPSAppSettings object.");
        return document;
    }

    private WorkspaceSettingsResponse Response(WorkspaceSettings settings)
    {
        var hasToken = !string.IsNullOrEmpty(settings.InfluxDB?.Token);
        if (settings.InfluxDB != null) settings.InfluxDB.Token = null;
        return new(Path.GetFullPath(filePath), settings, hasToken);
    }

    private static void Validate<T>(T? options, string section, IValidator<T> validator, List<ValidationFailure> failures) where T : class
    {
        if (options == null)
        {
            failures.Add(new(section, $"{section} settings are required."));
            return;
        }
        failures.AddRange(validator.Validate(options).Errors.Select(error =>
            new ValidationFailure(string.IsNullOrEmpty(error.PropertyName) ? section : $"{section}.{error.PropertyName}", error.ErrorMessage)));
    }
}