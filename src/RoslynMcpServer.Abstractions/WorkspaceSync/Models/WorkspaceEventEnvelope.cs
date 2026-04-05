namespace RoslynMcpServer.Abstractions.WorkspaceSync.Models;

/// <summary>
/// Represents a normalized synchronization signal emitted by any workspace event source.
/// </summary>
public sealed record WorkspaceEventEnvelope(
    string WorkspacePath,
    string? FilePath,
    string? OldFilePath,
    DateTimeOffset OccurredAtUtc,
    ChangeOrigin Origin,
    WorkspaceFileEventKind Kind,
    string? OperationId = null,
    string? ExpectedTextHash = null,
    string? Reason = null);
