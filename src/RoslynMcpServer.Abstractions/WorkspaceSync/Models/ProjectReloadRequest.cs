namespace RoslynMcpServer.Abstractions.WorkspaceSync.Models;

/// <summary>
/// Identifies a project path that should be reloaded when project-scoped reload is supported.
/// </summary>
public sealed record ProjectReloadRequest(
    string ProjectPath,
    string? Reason = null);
