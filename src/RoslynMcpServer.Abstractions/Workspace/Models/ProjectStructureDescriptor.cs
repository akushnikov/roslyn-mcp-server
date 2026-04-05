namespace RoslynMcpServer.Abstractions.Workspace.Models;

/// <summary>
/// Describes a project together with its source documents.
/// </summary>
public sealed record ProjectStructureDescriptor(
    string Name,
    string? FilePath,
    IReadOnlyList<string> TargetFrameworks,
    int DocumentCount,
    IReadOnlyList<string> ProjectReferences,
    IReadOnlyList<DocumentDescriptor> Documents);
