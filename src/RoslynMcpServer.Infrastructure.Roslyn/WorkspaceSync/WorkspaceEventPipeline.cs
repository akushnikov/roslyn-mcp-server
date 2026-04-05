using System.Reactive.Linq;
using Microsoft.Extensions.Logging;
using RoslynMcpServer.Abstractions.WorkspaceSync.Models;

namespace RoslynMcpServer.Infrastructure.Roslyn.WorkspaceSync;

/// <summary>
/// Merges workspace event sources into buffered delta requests for the serialized apply loop.
/// </summary>
internal sealed class WorkspaceEventPipeline(
    WorkspaceChangePolicy changePolicy,
    ILogger<WorkspaceEventPipeline> logger)
{
    private static readonly TimeSpan DefaultBufferWindow = TimeSpan.FromMilliseconds(150);
    private const int MaxBatchSize = 256;

    /// <summary>
    /// Creates the merged event stream for a single workspace.
    /// </summary>
    public IObservable<WorkspaceDelta> Create(
        string workspacePath,
        IObservable<WorkspaceEventEnvelope> fileSystemEvents,
        IObservable<WorkspaceEventEnvelope> roslynEvents,
        IObservable<WorkspaceEventEnvelope> reconcileEvents,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspacePath);
        ArgumentNullException.ThrowIfNull(fileSystemEvents);
        ArgumentNullException.ThrowIfNull(roslynEvents);
        ArgumentNullException.ThrowIfNull(reconcileEvents);

        logger.LogDebug("Creating merged workspace event pipeline for {WorkspacePath}", workspacePath);

        return Observable.Merge(fileSystemEvents, roslynEvents, reconcileEvents)
            .Where(_ => !cancellationToken.IsCancellationRequested)
            .Select(NormalizeEnvelope)
            .Buffer(DefaultBufferWindow, MaxBatchSize)
            .Where(batch => batch.Count > 0)
            .Select(CoalesceBatch)
            .Where(batch => batch.Count > 0)
            .SelectMany(batch => Observable.FromAsync(ct => changePolicy.EvaluateAsync(workspacePath, batch, ct).AsTask()));
    }

    private static WorkspaceEventEnvelope NormalizeEnvelope(WorkspaceEventEnvelope envelope)
    {
        return envelope with
        {
            WorkspacePath = NormalizePath(envelope.WorkspacePath),
            FilePath = NormalizePathOrNull(envelope.FilePath),
            OldFilePath = NormalizePathOrNull(envelope.OldFilePath)
        };
    }

    private static List<WorkspaceEventEnvelope> CoalesceBatch(IList<WorkspaceEventEnvelope> batch)
    {
        var byKey = new Dictionary<string, WorkspaceEventEnvelope>(StringComparer.OrdinalIgnoreCase);

        foreach (var candidate in batch.OrderBy(static eventItem => GetPriority(eventItem.Kind)))
        {
            var key = candidate.OldFilePath
                ?? candidate.FilePath
                ?? $"{candidate.WorkspacePath}::{candidate.Kind}";

            if (!byKey.TryGetValue(key, out var existing) ||
                GetPriority(candidate.Kind) > GetPriority(existing.Kind) ||
                candidate.OccurredAtUtc >= existing.OccurredAtUtc)
            {
                byKey[key] = candidate;
            }
        }

        return byKey.Values
            .OrderByDescending(static eventItem => GetPriority(eventItem.Kind))
            .ThenBy(static eventItem => eventItem.FilePath ?? eventItem.OldFilePath ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static int GetPriority(WorkspaceFileEventKind kind) => kind switch
    {
        WorkspaceFileEventKind.Faulted => 6,
        WorkspaceFileEventKind.Renamed => 5,
        WorkspaceFileEventKind.Deleted => 4,
        WorkspaceFileEventKind.Created => 3,
        WorkspaceFileEventKind.WorkspacePatched => 2,
        WorkspaceFileEventKind.Changed => 1,
        WorkspaceFileEventKind.ReconcileRequested => 0,
        _ => 0
    };

    private static string NormalizePath(string path) => Path.GetFullPath(path);

    private static string? NormalizePathOrNull(string? path) =>
        string.IsNullOrWhiteSpace(path)
            ? null
            : NormalizePath(path);
}
