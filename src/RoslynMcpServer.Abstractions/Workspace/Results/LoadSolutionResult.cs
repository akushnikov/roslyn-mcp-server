using RoslynMcpServer.Abstractions.Workspace.Models;

namespace RoslynMcpServer.Abstractions.Workspace.Results;

/// <summary>
/// Represents the outcome of loading a solution or project into the workspace cache.
/// </summary>
public sealed record LoadSolutionResult(
    WorkspaceDescriptor? Workspace,
    bool WasAlreadyLoaded,
    bool WasReloaded,
    IReadOnlyList<WorkspaceOperationDiagnostic> Diagnostics,
    string? FailureReason,
    string? Guidance);
