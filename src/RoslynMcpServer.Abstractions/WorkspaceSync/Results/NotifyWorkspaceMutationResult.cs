namespace RoslynMcpServer.Abstractions.WorkspaceSync.Results;

/// <summary>
/// Represents the outcome of registering a Roslyn-originated workspace mutation.
/// </summary>
public sealed record NotifyWorkspaceMutationResult(
    bool Accepted,
    int RegisteredChangeCount,
    string? FailureReason,
    string? Guidance);
