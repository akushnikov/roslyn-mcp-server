namespace RoslynMcpServer.Abstractions.Workspace.Models;

/// <summary>
/// Describes a source document in a loaded project.
/// </summary>
public sealed record DocumentDescriptor(
    string Name,
    string? FilePath);
