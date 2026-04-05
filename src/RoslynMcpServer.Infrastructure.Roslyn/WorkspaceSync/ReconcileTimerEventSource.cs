using System.Reactive.Linq;
using RoslynMcpServer.Abstractions.WorkspaceSync.Models;

namespace RoslynMcpServer.Infrastructure.Roslyn.WorkspaceSync;

/// <summary>
/// Produces periodic reconciliation ticks for a workspace.
/// </summary>
internal sealed class ReconcileTimerEventSource
{
    private static readonly TimeSpan DefaultInterval = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Creates the reconcile stream for the specified workspace.
    /// </summary>
    public IObservable<WorkspaceEventEnvelope> Create(string workspacePath, TimeSpan? interval = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspacePath);

        var cadence = interval.GetValueOrDefault(DefaultInterval);
        return Observable.Interval(cadence)
            .Select(_ => new WorkspaceEventEnvelope(
                WorkspacePath: workspacePath,
                FilePath: null,
                OldFilePath: null,
                OccurredAtUtc: DateTimeOffset.UtcNow,
                Origin: ChangeOrigin.Reconcile,
                Kind: WorkspaceFileEventKind.ReconcileRequested,
                Reason: "Periodic reconcile tick."));
    }
}
