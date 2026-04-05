namespace RoslynMcpServer.Abstractions.Workspace.Models;

/// <summary>
/// Describes a project that belongs to a loaded workspace.
/// </summary>
public sealed record ProjectDescriptor(
    string Name,
    string? FilePath,
    IReadOnlyList<string> TargetFrameworks,
    int DocumentCount,
    IReadOnlyList<string> ProjectReferences);
