namespace RoslynMcpServer.Abstractions.WorkspaceSync.Models;

/// <summary>
/// Describes a document text update that can be applied without reloading project structure.
/// </summary>
public sealed record DocumentPatch(
    string FilePath,
    string? ExpectedTextHash,
    string? CurrentWorkspaceTextHash,
    string? CurrentDiskTextHash);
