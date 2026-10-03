using LPS.Infrastructure.Distributed;
using LPS.Infrastructure.Nodes;
using Microsoft.Extensions.DependencyInjection;

namespace LPS.UI.Core.Distributed;

internal static class DistributedHostServices
{
    public static void Register(IServiceCollection services, ClusterRunSettings settings)
    {
        services.AddSingleton(settings);
        services.AddSingleton<INodeRegistry, RunNodeRegistry>();
        services.AddSingleton<DistributedRunFinalizer>();
        if (settings.Role == NodeType.Master)
        {
            services.AddSingleton<MasterCoordinator>();
            services.AddSingleton<IWorkerCoordinator>(provider => provider.GetRequiredService<MasterCoordinator>());
            services.AddHostedService<MasterRunHostedService>();
        }
        else services.AddHostedService<WorkerRunHostedService>();
    }
}