using RoslynMcpServer.Abstractions.Infrastructure.Models;
using RoslynMcpServer.Abstractions.Infrastructure.Requests;
using RoslynMcpServer.Abstractions.Infrastructure.Results;
using RoslynMcpServer.Abstractions.Infrastructure.Services;
using RoslynMcpServer.Abstractions.Server.Services;
using RoslynMcpServer.Abstractions.Workspace.Results;

namespace RoslynMcpServer.Application.Infrastructure;

/// <summary>
/// Orchestrates environment and workspace diagnostics for the MCP server.
/// </summary>
public sealed class ServerDiagnosticsService(
    IServerInfoService serverInfoService,
    IRuntimeEnvironmentService runtimeEnvironmentService) : IServerDiagnosticsService
{
    /// <inheritdoc />
    public async ValueTask<DiagnoseResult> DiagnoseAsync(
        DiagnoseRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var server = await serverInfoService.GetServerInfoAsync(cancellationToken);
        var environment = await runtimeEnvironmentService.GetEnvironmentAsync(cancellationToken);

        WorkspaceHealthDescriptor? workspace = null;
        IReadOnlyList<WorkspaceOperationDiagnostic> diagnostics = Array.Empty<WorkspaceOperationDiagnostic>();

        if (!string.IsNullOrWhiteSpace(request.SolutionPath))
        {
            workspace = await runtimeEnvironmentService.ProbeWorkspaceAsync(request.SolutionPath, cancellationToken);
            diagnostics = request.Verbose
                ? workspace.Diagnostics
                : workspace.Diagnostics
                    .Where(static diagnostic => diagnostic.Severity == WorkspaceDiagnosticSeverity.Error)
                    .ToArray();
        }

        return new DiagnoseResult(server, environment, workspace, diagnostics);
    }
}
