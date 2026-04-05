namespace RoslynMcpServer.Abstractions.WorkspaceSync.Models;

/// <summary>
/// Identifies where a workspace synchronization signal originated.
/// </summary>
public enum ChangeOrigin
{
    /// <summary>
    /// The signal came from an external file system event.
    /// </summary>
    FileSystem = 0,

    /// <summary>
    /// The signal came from an in-process Roslyn mutation.
    /// </summary>
    RoslynInternal = 1,

    /// <summary>
    /// The signal came from periodic reconciliation logic.
    /// </summary>
    Reconcile = 2
}
