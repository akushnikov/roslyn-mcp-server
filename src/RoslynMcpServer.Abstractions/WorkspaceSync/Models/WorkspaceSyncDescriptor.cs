using System.Collections.Immutable;

namespace RoslynMcpServer.Abstractions.WorkspaceSync.Models;

/// <summary>
/// Describes the observable synchronization state for a loaded workspace.
/// </summary>
public sealed record WorkspaceSyncDescriptor(
    string WorkspacePath,
    bool IsRunning,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset? LastEventAtUtc,
    DateTimeOffset? LastReconcileAtUtc,
    int TrackedDocumentCount,
    int PendingExpectedExternalChangeCount,
    ImmutableArray<TrackedDocumentDescriptor> Documents);
