using RoslynMcpServer.Abstractions.WorkspaceSync.Requests;
using RoslynMcpServer.Abstractions.WorkspaceSync.Results;

namespace RoslynMcpServer.Abstractions.WorkspaceSync.Services;

/// <summary>
/// Registers Roslyn-originated mutations so file system echo can be correlated with the initiating operation.
/// </summary>
public interface IWorkspaceMutationNotifier
{
    /// <summary>
    /// Stores the expected external changes that should appear after an in-process Roslyn mutation.
    /// </summary>
    ValueTask<NotifyWorkspaceMutationResult> NotifyAsync(
        NotifyWorkspaceMutationRequest request,
        CancellationToken cancellationToken = default);
}
