using RoslynMcpServer.Abstractions.Workspace.Results;

namespace RoslynMcpServer.Infrastructure.Roslyn.WorkspaceSync;

/// <summary>
/// Represents the outcome of a workspace reload operation.
/// </summary>
internal sealed record WorkspaceReloadResult(
    bool Succeeded,
    bool RequiresCoordinatorRefresh,
    string? FailureReason,
    IReadOnlyList<WorkspaceOperationDiagnostic> Diagnostics);
