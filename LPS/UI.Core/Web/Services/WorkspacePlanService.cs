using System.Text.Json;
using FluentValidation;
using FluentValidation.Results;
using LPS.UI.Common.DTOs;
using LPS.UI.Core.LPSValidators;
using LPS.UI.Core.Web.Models;

namespace LPS.UI.Core.Web.Services;

public sealed class WorkspacePlanService : IWorkspacePlanService
{
    private readonly string _directory;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    public WorkspacePlanService(WorkspaceOptions options)
    {
        _directory = Path.Combine(Path.GetFullPath(options.DataDirectory), "plans");
        Directory.CreateDirectory(_directory);
    }

    public async Task<IReadOnlyList<WorkspacePlan>> ListAsync(CancellationToken token)
    {
        var plans = new List<WorkspacePlan>();
        foreach (var path in Directory.EnumerateFiles(_directory, "*.json"))
        {
            if (Guid.TryParse(Path.GetFileNameWithoutExtension(path), out var id))
                plans.Add(await GetAsync(id, token));
        }
        return plans.OrderByDescending(plan => plan.UpdatedAt).ToArray();
    }

    public async Task<WorkspacePlan> GetAsync(Guid id, CancellationToken token)
    {
        var path = Path.Combine(_directory, $"{id}.json");
        if (!File.Exists(path))
            throw new KeyNotFoundException("Plan not found.");
        return JsonSerializer.Deserialize<WorkspacePlan>(await File.ReadAllTextAsync(path, token), JsonOptions)
            ?? throw new InvalidDataException("The saved plan is empty.");
    }

    public async Task<WorkspacePlan> SaveAsync(Guid? id, PlanDto plan, CancellationToken token)
    {
        if (plan.Rounds == null || plan.Iterations == null || plan.Variables == null || plan.Environments == null
            || plan.Rounds.Any(round => round == null || round.Iterations == null || round.ReferencedIterations == null
                || round.Iterations.Any(iteration => iteration?.HttpRequest == null))
            || plan.Iterations.Any(iteration => iteration?.HttpRequest == null)
            || plan.Variables.Any(variable => variable == null) || plan.Environments.Any(environment => environment == null))
            throw new ValidationException(new[] { new ValidationFailure("Plan", "Plan collections cannot be null and every iteration requires an HTTP request.") });
        var validation = new PlanValidator(plan).Validate();
        if (!plan.Rounds.Any(round => round.Iterations.Count > 0 || round.ReferencedIterations.Count > 0) && plan.Iterations.Count == 0)
            validation.Errors.Add(new ValidationFailure("Rounds", "Add at least one request to the plan."));
        if (!validation.IsValid)
            throw new ValidationException(validation.Errors);
        if (id.HasValue)
            await GetAsync(id.Value, token);

        var saved = new WorkspacePlan(id ?? Guid.NewGuid(), DateTimeOffset.UtcNow, plan);
        var path = Path.Combine(_directory, $"{saved.Id}.json");
        var temporaryPath = $"{path}.{Guid.NewGuid():N}.tmp";
        try
        {
            await File.WriteAllTextAsync(temporaryPath, JsonSerializer.Serialize(saved, JsonOptions), token);
            File.Move(temporaryPath, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
                File.Delete(temporaryPath);
        }
        return saved;
    }

    public async Task DeleteAsync(Guid id, CancellationToken token)
    {
        await GetAsync(id, token);
        File.Delete(Path.Combine(_directory, $"{id}.json"));
    }
}