namespace RoslynMcpServer.Abstractions.WorkspaceSync.Models;

/// <summary>
/// Describes the synchronization state currently tracked for a workspace document.
/// </summary>
public sealed record TrackedDocumentDescriptor(
    string FilePath,
    string DocumentKey,
    string? WorkspaceTextHash,
    string? DiskTextHash,
    string? PendingOperationId,
    DateTimeOffset? LastSynchronizedAtUtc,
    DateTimeOffset? SuppressedUntilUtc);
