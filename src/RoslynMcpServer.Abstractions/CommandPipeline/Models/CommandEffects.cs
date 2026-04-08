namespace RoslynMcpServer.Abstractions.CommandPipeline.Models;

/// <summary>
/// Minimal side-effect metadata for stateful command execution.
/// </summary>
/// <param name="Changed">Whether command execution changed workspace state.</param>
/// <param name="WorkspaceVersionBefore">Workspace version marker before execution, when available.</param>
/// <param name="WorkspaceVersionAfter">Workspace version marker after execution, when available.</param>
/// <param name="NormalizedSolutionPath">Normalized workspace path targeted by the command.</param>
public sealed record CommandEffects(
    bool Changed,
    long? WorkspaceVersionBefore,
    long? WorkspaceVersionAfter,
    string? NormalizedSolutionPath);
