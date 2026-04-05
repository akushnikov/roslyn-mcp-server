namespace RoslynMcpServer.Abstractions.Workspace.Models;

/// <summary>
/// Represents a client-provided workspace root and its optional local file system path.
/// </summary>
public sealed record WorkspaceRoot(
    string Uri,
    string? Name,
    string? LocalPath);
