using RoslynMcpServer.Abstractions.Workspace.Results;

namespace RoslynMcpServer.Abstractions.Infrastructure.Models;

/// <summary>
/// Describes the result of validating or probing a workspace path.
/// </summary>
public sealed record WorkspaceHealthDescriptor(
    string? SolutionPath,
    bool CanBeLoaded,
    int ProjectCount,
    int DocumentCount,
    IReadOnlyList<WorkspaceOperationDiagnostic> Diagnostics);
