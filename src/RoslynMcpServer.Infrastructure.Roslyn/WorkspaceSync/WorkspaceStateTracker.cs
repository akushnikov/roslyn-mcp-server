using System.Collections.Concurrent;
using System.Collections.Immutable;
using RoslynMcpServer.Abstractions.WorkspaceSync.Models;

namespace RoslynMcpServer.Infrastructure.Roslyn.WorkspaceSync;

/// <summary>
/// Holds the transport-safe synchronization state exposed by a workspace coordinator.
/// </summary>
internal sealed class WorkspaceStateTracker
{
    private readonly ConcurrentDictionary<string, TrackedDocumentDescriptor> _documents = new(StringComparer.OrdinalIgnoreCase);
    private DateTimeOffset _startedAtUtc;
    private DateTimeOffset? _lastEventAtUtc;
    private DateTimeOffset? _lastReconcileAtUtc;
    private DateTimeOffset? _lastAppliedAtUtc;
    private int _pendingExpectedExternalChangeCount;

    /// <summary>
    /// Marks the coordinator as started and records the start timestamp.
    /// </summary>
    public void MarkStarted()
    {
        _startedAtUtc = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Records the latest observed event timestamp.
    /// </summary>
    public void MarkEventObserved(DateTimeOffset occurredAtUtc)
    {
        _lastEventAtUtc = occurredAtUtc;
    }

    /// <summary>
    /// Records the latest reconcile completion timestamp.
    /// </summary>
    public void MarkReconciled(DateTimeOffset reconciledAtUtc)
    {
        _lastReconcileAtUtc = reconciledAtUtc;
    }

    /// <summary>
    /// Records the latest successful apply-loop completion timestamp.
    /// </summary>
    public void MarkApplied(DateTimeOffset appliedAtUtc)
    {
        _lastAppliedAtUtc = appliedAtUtc;
    }

    /// <summary>
    /// Replaces the tracked count of pending expected external changes.
    /// </summary>
    public void ReplaceExpectedExternalChangeCount(int count)
    {
        _pendingExpectedExternalChangeCount = count;
    }

    /// <summary>
    /// Updates the tracked state for a single synchronized document.
    /// </summary>
    public void UpsertDocument(
        string filePath,
        string documentKey,
        string? workspaceTextHash,
        string? diskTextHash,
        string? pendingOperationId,
        DateTimeOffset? lastSynchronizedAtUtc,
        DateTimeOffset? suppressedUntilUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(documentKey);

        _documents[NormalizePath(filePath)] = new TrackedDocumentDescriptor(
            FilePath: NormalizePath(filePath),
            DocumentKey: documentKey,
            WorkspaceTextHash: workspaceTextHash,
            DiskTextHash: diskTextHash,
            PendingOperationId: pendingOperationId,
            LastSynchronizedAtUtc: lastSynchronizedAtUtc,
            SuppressedUntilUtc: suppressedUntilUtc);
    }

    /// <summary>
    /// Returns the tracked descriptor for the specified file path when present.
    /// </summary>
    public bool TryGetDocument(string filePath, out TrackedDocumentDescriptor? document)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        var found = _documents.TryGetValue(NormalizePath(filePath), out var descriptor);
        document = descriptor;
        return found;
    }

    /// <summary>
    /// Marks the file as awaiting an expected external echo from a Roslyn-originated mutation.
    /// </summary>
    public void MarkExpectedExternalChange(
        string filePath,
        string operationId,
        DateTimeOffset suppressedUntilUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(operationId);

        var normalizedPath = NormalizePath(filePath);
        if (_documents.TryGetValue(normalizedPath, out var existing))
        {
            _documents[normalizedPath] = existing with
            {
                PendingOperationId = operationId,
                SuppressedUntilUtc = suppressedUntilUtc
            };
            return;
        }

        _documents[normalizedPath] = new TrackedDocumentDescriptor(
            FilePath: normalizedPath,
            DocumentKey: normalizedPath,
            WorkspaceTextHash: null,
            DiskTextHash: null,
            PendingOperationId: operationId,
            LastSynchronizedAtUtc: null,
            SuppressedUntilUtc: suppressedUntilUtc);
    }

    /// <summary>
    /// Clears all currently tracked document state.
    /// </summary>
    public void ClearDocuments()
    {
        _documents.Clear();
    }

    /// <summary>
    /// Returns a stable snapshot of the tracked documents.
    /// </summary>
    public IReadOnlyList<TrackedDocumentDescriptor> SnapshotDocuments()
    {
        return _documents.Values
            .OrderBy(static document => document.FilePath, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    /// <summary>
    /// Creates a transport-safe descriptor for the current coordinator state.
    /// </summary>
    public WorkspaceSyncDescriptor Describe(string workspacePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspacePath);

        return new WorkspaceSyncDescriptor(
            WorkspacePath: workspacePath,
            IsRunning: _startedAtUtc != default,
            StartedAtUtc: _startedAtUtc == default ? DateTimeOffset.UtcNow : _startedAtUtc,
            LastEventAtUtc: _lastEventAtUtc,
            LastReconcileAtUtc: _lastAppliedAtUtc > _lastReconcileAtUtc ? _lastAppliedAtUtc : _lastReconcileAtUtc,
            TrackedDocumentCount: _documents.Count,
            PendingExpectedExternalChangeCount: _pendingExpectedExternalChangeCount,
            Documents: _documents.Values
                .OrderBy(static document => document.FilePath, StringComparer.OrdinalIgnoreCase)
                .ToImmutableArray());
    }

    private static string NormalizePath(string path) => Path.GetFullPath(path);
}
