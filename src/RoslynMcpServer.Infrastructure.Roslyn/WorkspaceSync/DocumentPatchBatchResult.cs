namespace RoslynMcpServer.Infrastructure.Roslyn.WorkspaceSync;

/// <summary>
/// Represents the outcome of applying a batch of document patch candidates.
/// </summary>
internal sealed record DocumentPatchBatchResult(
    int AppliedCount,
    int SkippedCount,
    bool RequiresSolutionReload,
    string? FailureReason);
