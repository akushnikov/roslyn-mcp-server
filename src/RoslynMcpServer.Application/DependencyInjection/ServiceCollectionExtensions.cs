using Microsoft.Extensions.DependencyInjection;
using RoslynMcpServer.Abstractions.Infrastructure.Services;
using RoslynMcpServer.Abstractions.Server.Services;
using RoslynMcpServer.Abstractions.Workspace.Services;
using RoslynMcpServer.Application.Infrastructure;
using RoslynMcpServer.Application.Server;
using RoslynMcpServer.Application.Workspace;

namespace RoslynMcpServer.Application.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddRoslynMcpServerApplication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<IServerInfoService, ServerInfoService>();
        services.AddSingleton<IServerDiagnosticsService, ServerDiagnosticsService>();
        services.AddSingleton<IWorkspaceContextResolver, WorkspaceContextResolver>();
        services.AddSingleton<IWorkspaceStateService, WorkspaceStateService>();
        services.AddSingleton<IProjectStructureService, ProjectStructureService>();
        return services;
    }
}
