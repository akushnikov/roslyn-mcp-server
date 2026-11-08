using Microsoft.Extensions.DependencyInjection;
using RoslynMcpServer.Abstractions.Analysis.Services;
using RoslynMcpServer.Application.Analysis;
using RoslynMcpServer.Application.Analysis.Operations;
using RoslynMcpServer.Abstractions.Infrastructure.Services;
using RoslynMcpServer.Abstractions.Navigation.Services;
using RoslynMcpServer.Abstractions.Server.Services;
using RoslynMcpServer.Abstractions.Workspace.Services;
using RoslynMcpServer.Application.Infrastructure;
using RoslynMcpServer.Application.Infrastructure.Operations;
using RoslynMcpServer.Application.Navigation;
using RoslynMcpServer.Application.Navigation.Operations;
using RoslynMcpServer.Application.Server;
using RoslynMcpServer.Application.Server.Operations;
using RoslynMcpServer.Application.Telemetry;
using RoslynMcpServer.Application.Workspace;
using RoslynMcpServer.Application.Workspace.Operations;

namespace RoslynMcpServer.Application.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddRoslynMcpServerApplication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<ServerInfoQueryOperation>();
        services.AddSingleton<IServerInfoQueryService, ServerInfoQueryService>();
        services.AddSingleton<DiagnoseQueryOperation>();
        services.AddSingleton<IDiagnoseQueryService, DiagnoseQueryService>();
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
        services.AddSingleton<DiagnosticsQueryOperation>();
        services.AddSingleton<IDiagnosticsQueryService, DiagnosticsQueryService>();
        services.AddSingleton<FindCallersQueryOperation>();
        services.AddSingleton<IFindCallersQueryService, FindCallersQueryService>();
        services.AddSingleton<FindImplementationsQueryOperation>();
        services.AddSingleton<IFindImplementationsQueryService, FindImplementationsQueryService>();
        services.AddSingleton<TypeHierarchyQueryOperation>();
        services.AddSingleton<ITypeHierarchyQueryService, TypeHierarchyQueryService>();
        services.AddSingleton<MethodAnalysisQueryOperation>();
        services.AddSingleton<IMethodAnalysisQueryService, MethodAnalysisQueryService>();
        services.AddSingleton<ValidateCodeQueryOperation>();
        services.AddSingleton<IValidateCodeQueryService, ValidateCodeQueryService>();
        services.AddSingleton<OutgoingCallsQueryOperation>();
        services.AddSingleton<IOutgoingCallsQueryService, OutgoingCallsQueryService>();
        services.AddSingleton<ChangeImpactQueryOperation>();
        services.AddSingleton<IChangeImpactQueryService, ChangeImpactQueryService>();
        services.AddSingleton<TypeCompatibilityQueryOperation>();
        services.AddSingleton<ITypeCompatibilityQueryService, TypeCompatibilityQueryService>();
        return services;
    }
}