using Microsoft.Extensions.DependencyInjection;
using RoslynMcpServer.Abstractions.Analysis.Services;
using RoslynMcpServer.Abstractions.Navigation.Services;
using RoslynMcpServer.Abstractions.Infrastructure.Services;
using RoslynMcpServer.Abstractions.Workspace.Services;
using RoslynMcpServer.Abstractions.WorkspaceSync.Services;
using RoslynMcpServer.Infrastructure.Roslyn.Analysis;
using RoslynMcpServer.Infrastructure.Roslyn.Navigation;
using RoslynMcpServer.Infrastructure.Roslyn.Runtime;
using RoslynMcpServer.Infrastructure.Roslyn.Workspace;
using RoslynMcpServer.Infrastructure.Roslyn.WorkspaceSync;

namespace RoslynMcpServer.Infrastructure.Roslyn.DependencyInjection;

/// <summary>
/// Registers Roslyn-backed infrastructure services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers workspace loading, cache, and runtime probing services.
    /// </summary>
    public static IServiceCollection AddRoslynMcpServerRoslynInfrastructure(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<IWorkspaceLoader, MsBuildWorkspaceLoader>();
        services.AddSingleton<IWorkspaceCache>(static provider => (IWorkspaceCache)provider.GetRequiredService<IWorkspaceLoader>());
        services.AddSingleton<IWorkspaceProbeService>(static provider => (IWorkspaceProbeService)provider.GetRequiredService<IWorkspaceLoader>());
        services.AddSingleton<IRoslynWorkspaceAccessor>(static provider => (IRoslynWorkspaceAccessor)provider.GetRequiredService<IWorkspaceLoader>());
        services.AddSingleton<IWorkspaceSessionProvider, WorkspaceSessionProvider>();
        services.AddSingleton<IWorkspaceSnapshotProvider>(static provider => provider.GetRequiredService<IWorkspaceSessionProvider>());
        services.AddSingleton<WorkspaceCoordinatorRegistry>();
        services.AddSingleton<IWorkspaceSyncService>(static provider => provider.GetRequiredService<WorkspaceCoordinatorRegistry>());
        services.AddSingleton<IWorkspaceMutationNotifier>(static provider => provider.GetRequiredService<WorkspaceCoordinatorRegistry>());
        services.AddSingleton<FileSystemWorkspaceEventSource>();
        services.AddSingleton<RoslynWorkspaceEventSource>();
        services.AddSingleton<ReconcileTimerEventSource>();
        services.AddSingleton<WorkspaceEventPipeline>();
        services.AddSingleton<WorkspaceChangePolicy>();
        services.AddSingleton<WorkspaceDocumentComparisonService>();
        services.AddSingleton<DocumentPatchService>();
        services.AddSingleton<SolutionReloadService>();
        services.AddSingleton<ProjectReloadService>();
        services.AddTransient<WorkspaceStateTracker>();
        services.AddTransient<ExpectedExternalChangeStore>();
        services.AddTransient<WorkspacePathIndex>();
        services.AddSingleton<INavigationService, RoslynNavigationService>();
        services.AddSingleton<IAnalysisService, RoslynAnalysisService>();
        services.AddSingleton<IRuntimeEnvironmentService, RuntimeEnvironmentService>();

        return services;
    }
}
