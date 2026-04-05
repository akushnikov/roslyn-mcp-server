namespace RoslynMcpServer.Infrastructure.Roslyn.WorkspaceSync;

/// <summary>
/// Represents the actual content state for a workspace document compared with the file on disk.
/// </summary>
internal sealed record WorkspaceDocumentComparison(
    string FilePath,
    bool DocumentExistsInWorkspace,
    bool DiskFileExists,
    bool DiskReadSucceeded,
    string? DocumentKey,
    string? WorkspaceText,
    string? DiskText,
    string? WorkspaceTextHash,
    string? DiskTextHash)
{
    /// <summary>
    /// Gets a value indicating whether both sources are available and their text content matches.
    /// </summary>
    public bool IsSynchronized =>
        DocumentExistsInWorkspace &&
        DiskFileExists &&
        DiskReadSucceeded &&
        WorkspaceTextHash is not null &&
        DiskTextHash is not null &&
        string.Equals(WorkspaceTextHash, DiskTextHash, StringComparison.Ordinal);
}
