namespace RoslynMcpServer.Abstractions.WorkspaceSync.Models;

/// <summary>
/// Describes the normalized kind of file-level signal observed by workspace synchronization.
/// </summary>
public enum WorkspaceFileEventKind
{
    /// <summary>
    /// The file content may have changed.
    /// </summary>
    Changed = 0,

    /// <summary>
    /// The file was created.
    /// </summary>
    Created = 1,

    /// <summary>
    /// The file was deleted.
    /// </summary>
    Deleted = 2,

    /// <summary>
    /// The file was renamed.
    /// </summary>
    Renamed = 3,

    /// <summary>
    /// The watcher reported an error and state should be reconciled conservatively.
    /// </summary>
    Faulted = 4,

    /// <summary>
    /// A reconcile tick requested a consistency check without naming a specific file.
    /// </summary>
    ReconcileRequested = 5,

    /// <summary>
    /// A Roslyn mutation already changed workspace state and may later echo through the watcher.
    /// </summary>
    WorkspacePatched = 6
}
