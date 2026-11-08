using Microsoft.Extensions.DependencyInjection;
using RoslynMcpServer.Abstractions.Infrastructure.Services;
using RoslynMcpServer.Abstractions.Navigation.Services;
using RoslynMcpServer.Abstractions.Server.Services;
using RoslynMcpServer.Abstractions.Workspace.Services;
using RoslynMcpServer.Application.Infrastructure;
using RoslynMcpServer.Application.Navigation;
using RoslynMcpServer.Application.Navigation.Operations;
using RoslynMcpServer.Application.Server;
using RoslynMcpServer.Application.Telemetry;
using RoslynMcpServer.Application.Workspace;
using RoslynMcpServer.Application.Workspace.Operations;

namespace RoslynMcpServer.Application.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddRoslynMcpServerApplication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<IServerInfoService, ServerInfoService>();
        services.AddSingleton<IServerDiagnosticsService, ServerDiagnosticsService>();
        services.AddSingleton<ResolveWorkspaceContextQueryOperation>();
        services.AddSingleton<IWorkspaceContextResolver, WorkspaceContextResolver>();
        services.AddSingleton<WorkspaceStateQueryOperation>();
        services.AddSingleton<IWorkspaceStateService, WorkspaceStateService>();
        services.AddSingleton<ProjectStructureQueryOperation>();
        services.AddSingleton<IProjectStructureService, ProjectStructureService>();
        services.AddSingleton<LoadSolutionCommandOperation>();
        services.AddSingleton<ILoadSolutionCommandService, LoadSolutionCommandService>();
        services.AddSingleton<SymbolInfoQueryOperation>();
        services.AddSingleton<ISymbolInfoQueryService, SymbolInfoQueryService>();
        services.AddSingleton<GoToDefinitionQueryOperation>();
        services.AddSingleton<IGoToDefinitionQueryService, GoToDefinitionQueryService>();
        services.AddSingleton<FindReferencesQueryOperation>();
        services.AddSingleton<IFindReferencesQueryService, FindReferencesQueryService>();
        services.AddSingleton<SearchSymbolsQueryOperation>();
        services.AddSingleton<ISearchSymbolsQueryService, SearchSymbolsQueryService>();
        services.AddSingleton<TypeMembersQueryOperation>();
        services.AddSingleton<ITypeMembersQueryService, TypeMembersQueryService>();
        services.AddSingleton<TypeOverviewQueryOperation>();
        services.AddSingleton<ITypeOverviewQueryService, TypeOverviewQueryService>();
        services.AddSingleton<SymbolAttributesQueryOperation>();
        services.AddSingleton<ISymbolAttributesQueryService, SymbolAttributesQueryService>();
        services.AddSingleton<MethodSignatureQueryOperation>();
        services.AddSingleton<IMethodSignatureQueryService, MethodSignatureQueryService>();
        services.AddSingleton<MethodSourceQueryOperation>();
        services.AddSingleton<IMethodSourceQueryService, MethodSourceQueryService>();
        services.AddSingleton<FileOverviewQueryOperation>();
        services.AddSingleton<IFileOverviewQueryService, FileOverviewQueryService>();
        services.AddSingleton<DocumentOutlineQueryOperation>();
        services.AddSingleton<IDocumentOutlineQueryService, DocumentOutlineQueryService>();
        return services;
    }
}
