using System.Collections.Immutable;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using RoslynMcpServer.Abstractions.WorkspaceSync.Models;
using RoslynMcpServer.Infrastructure.Roslyn.Workspace;

namespace RoslynMcpServer.Infrastructure.Roslyn.WorkspaceSync;

/// <summary>
/// Owns synchronization state and lifecycle for a single loaded workspace.
/// </summary>
internal sealed class WorkspaceCoordinator(
    string workspacePath,
    FileSystemWorkspaceEventSource fileSystemEventSource,
    RoslynWorkspaceEventSource roslynWorkspaceEventSource,
    ReconcileTimerEventSource reconcileTimerEventSource,
    WorkspaceEventPipeline eventPipeline,
    DocumentPatchService documentPatchService,
    WorkspaceDocumentComparisonService comparisonService,
    SolutionReloadService solutionReloadService,
    ProjectReloadService projectReloadService,
    IRoslynWorkspaceAccessor workspaceAccessor,
    WorkspacePathIndex workspacePathIndex,
    WorkspaceStateTracker stateTracker,
    ExpectedExternalChangeStore expectedExternalChangeStore,
    ILogger<WorkspaceCoordinator> logger) : IAsyncDisposable
{
    private const int ReconcilePatchEscalationThreshold = 64;
    private readonly string _workspacePath = NormalizePath(workspacePath);
    private readonly Lock _gate = new();
    private readonly WorkspaceStateTracker _stateTracker = stateTracker;
    private readonly ExpectedExternalChangeStore _expectedExternalChangeStore = expectedExternalChangeStore;
    private readonly object _quiescenceGate = new();
    private readonly SemaphoreSlim _applySemaphore = new(1, 1);
    private Channel<WorkspaceDelta>? _pendingDeltas;
    private IDisposable? _subscription;
    private CancellationTokenSource? _lifetimeCts;
    private Task? _applyLoop;
    private TaskCompletionSource _quiescenceTcs = CreateCompletedQuiescenceSource();
    private int _pendingDeltaCount;

    /// <summary>
    /// Gets the normalized workspace root path handled by this coordinator.
    /// </summary>
    public string WorkspacePath => _workspacePath;

    /// <summary>
    /// Starts the coordinator if it is not already running.
    /// </summary>
    public ValueTask<bool> StartAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_gate)
        {
            if (_lifetimeCts is not null)
            {
                return ValueTask.FromResult(false);
            }

            _pendingDeltas = Channel.CreateUnbounded<WorkspaceDelta>(
                new UnboundedChannelOptions
                {
                    SingleReader = true,
                    SingleWriter = false
                });
            _lifetimeCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _pendingDeltaCount = 0;
            _quiescenceTcs = CreateCompletedQuiescenceSource();
            _stateTracker.MarkStarted();

            var stream = eventPipeline.Create(
                _workspacePath,
                fileSystemEventSource.Create(_workspacePath),
                roslynWorkspaceEventSource.Create(_workspacePath),
                reconcileTimerEventSource.Create(_workspacePath),
                _lifetimeCts.Token);

            _subscription = stream.Subscribe(delta =>
            {
                _stateTracker.MarkEventObserved(DateTimeOffset.UtcNow);
                if (_pendingDeltas.Writer.TryWrite(delta))
                {
                    MarkDeltaQueued();
                }
            });

            InitializeTrackedStateAsync(_lifetimeCts.Token).GetAwaiter().GetResult();
            _applyLoop = RunApplyLoopAsync(_lifetimeCts.Token);
            logger.LogInformation("Workspace sync coordinator started for {WorkspacePath}", _workspacePath);
            return ValueTask.FromResult(true);
        }
    }

    /// <summary>
    /// Stops the coordinator if it is running.
    /// </summary>
    public async ValueTask<bool> StopAsync(CancellationToken cancellationToken = default)
    {
        CancellationTokenSource? lifetimeCts;
        Task? applyLoop;
        IDisposable? subscription;
        Channel<WorkspaceDelta>? pendingDeltas;

        lock (_gate)
        {
            if (_lifetimeCts is null)
            {
                return false;
            }

            lifetimeCts = _lifetimeCts;
            applyLoop = _applyLoop;
            subscription = _subscription;
            pendingDeltas = _pendingDeltas;

            _lifetimeCts = null;
            _applyLoop = null;
            _subscription = null;
            _pendingDeltas = null;
        }

        subscription?.Dispose();
        lifetimeCts!.Cancel();
        pendingDeltas?.Writer.TryComplete();

        if (applyLoop is not null)
        {
            try
            {
                await applyLoop.WaitAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
            }
        }

        lifetimeCts.Dispose();
        logger.LogInformation("Workspace sync coordinator stopped for {WorkspacePath}", _workspacePath);
        return true;
    }

    /// <summary>
    /// Registers Roslyn-originated expected external changes for echo suppression.
    /// </summary>
    public ValueTask<int> NotifyRoslynMutationAsync(
        ImmutableArray<ExpectedExternalChange> expectedExternalChanges,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        _expectedExternalChangeStore.Store(expectedExternalChanges);

        foreach (var expectedExternalChange in expectedExternalChanges)
        {
            _stateTracker.MarkExpectedExternalChange(
                expectedExternalChange.FilePath,
                expectedExternalChange.OperationId,
                expectedExternalChange.ExpiresAtUtc);
        }

        _stateTracker.ReplaceExpectedExternalChangeCount(_expectedExternalChangeStore.Count);

        return ValueTask.FromResult(expectedExternalChanges.Length);
    }

    /// <summary>
    /// Returns a transport-safe snapshot of the coordinator state.
    /// </summary>
    public WorkspaceSyncDescriptor Describe() => _stateTracker.Describe(_workspacePath);

    /// <summary>
    /// Waits until the coordinator has no queued or in-flight mutations.
    /// </summary>
    public async ValueTask WaitForQuiescenceAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        Task quiescenceTask;
        lock (_quiescenceGate)
        {
            quiescenceTask = _quiescenceTcs.Task;
        }

        await quiescenceTask.WaitAsync(cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
    }

    private async Task RunApplyLoopAsync(CancellationToken cancellationToken)
    {
        var reader = _pendingDeltas?.Reader;
        if (reader is null)
        {
            return;
        }

        try
        {
            await foreach (var delta in reader.ReadAllAsync(cancellationToken))
            {
                var (mergedDelta, consumedCount) = MergePendingDeltas(delta, reader);
                await _applySemaphore.WaitAsync(cancellationToken);
                try
                {
                    await ApplyDeltaAsync(mergedDelta, cancellationToken);
                }
                finally
                {
                    _applySemaphore.Release();
                    MarkDeltaApplied(consumedCount);
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async ValueTask ApplyDeltaAsync(WorkspaceDelta delta, CancellationToken cancellationToken)
    {
        _stateTracker.MarkEventObserved(DateTimeOffset.UtcNow);
        _expectedExternalChangeStore.RemoveExpired(DateTimeOffset.UtcNow);
        _stateTracker.ReplaceExpectedExternalChangeCount(_expectedExternalChangeStore.Count);

        logger.LogDebug(
            "Applying merged workspace delta for {WorkspacePath}: reloadSolution={ReloadSolution}, patchCount={PatchCount}, projectReloadCount={ProjectReloadCount}, reconcileOnly={ReconcileOnly}",
            _workspacePath,
            delta.ReloadSolution,
            delta.PatchDocuments.Length,
            delta.ReloadProjects.Length,
            delta.ReconcileOnly);

        if (delta.ReconcileOnly)
        {
            delta = await CreateReconcileDeltaAsync(cancellationToken);
            logger.LogDebug(
                "Reconcile sweep produced delta for {WorkspacePath}: reloadSolution={ReloadSolution}, patchCount={PatchCount}",
                _workspacePath,
                delta.ReloadSolution,
                delta.PatchDocuments.Length);
        }

        if (delta.ReloadSolution)
        {
            var reloadResult = await solutionReloadService.HandleAsync(_workspacePath, delta.Reason, cancellationToken);
            if (!reloadResult.Succeeded)
            {
                logger.LogWarning(
                    "Solution reload failed for {WorkspacePath}: {FailureReason}",
                    _workspacePath,
                    reloadResult.FailureReason ?? "unknown failure");
                _stateTracker.MarkApplied(DateTimeOffset.UtcNow);
                _stateTracker.MarkReconciled(DateTimeOffset.UtcNow);
                return;
            }

            if (reloadResult.RequiresCoordinatorRefresh)
            {
                await RefreshAfterReloadAsync(cancellationToken);
            }

            _stateTracker.MarkApplied(DateTimeOffset.UtcNow);
            _stateTracker.MarkReconciled(DateTimeOffset.UtcNow);
            return;
        }

        if (delta.ReloadProjects.Length > 0)
        {
            var projectReloadResult = await projectReloadService.HandleAsync(_workspacePath, delta.ReloadProjects, cancellationToken);
            if (!projectReloadResult.Succeeded)
            {
                logger.LogWarning(
                    "Project reload failed for {WorkspacePath}: {FailureReason}",
                    _workspacePath,
                    projectReloadResult.FailureReason ?? "unknown failure");
                _stateTracker.MarkApplied(DateTimeOffset.UtcNow);
                _stateTracker.MarkReconciled(DateTimeOffset.UtcNow);
                return;
            }

            if (projectReloadResult.RequiresCoordinatorRefresh)
            {
                await RefreshAfterReloadAsync(cancellationToken);
            }
        }

        if (delta.PatchDocuments.Length > 0)
        {
            var patchResult = await documentPatchService.HandleAsync(
                _workspacePath,
                delta.PatchDocuments,
                _stateTracker,
                _expectedExternalChangeStore,
                cancellationToken);

            if (patchResult.RequiresSolutionReload)
            {
                var reloadResult = await solutionReloadService.HandleAsync(
                    _workspacePath,
                    patchResult.FailureReason ?? delta.Reason,
                    cancellationToken);

                if (reloadResult.Succeeded && reloadResult.RequiresCoordinatorRefresh)
                {
                    await RefreshAfterReloadAsync(cancellationToken);
                }

                _stateTracker.MarkApplied(DateTimeOffset.UtcNow);
                _stateTracker.MarkReconciled(DateTimeOffset.UtcNow);
                return;
            }
        }

        if (delta.ReconcileOnly || delta.ReloadProjects.Length > 0 || delta.PatchDocuments.Length > 0)
        {
            _stateTracker.MarkReconciled(DateTimeOffset.UtcNow);
        }

        _stateTracker.MarkApplied(DateTimeOffset.UtcNow);
    }

    private async ValueTask<WorkspaceDelta> CreateReconcileDeltaAsync(CancellationToken cancellationToken)
    {
        var trackedDocuments = _stateTracker.SnapshotDocuments();
        if (trackedDocuments.Count == 0)
        {
            return new WorkspaceDelta(
                PatchDocuments: ImmutableArray<DocumentPatch>.Empty,
                ReloadProjects: ImmutableArray<ProjectReloadRequest>.Empty,
                ReloadSolution: false,
                ReconcileOnly: true,
                Origin: ChangeOrigin.Reconcile,
                Reason: "Reconcile sweep found no tracked documents.");
        }

        var patchDocuments = ImmutableArray.CreateBuilder<DocumentPatch>();

        foreach (var trackedDocument in trackedDocuments)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var comparison = await comparisonService.CompareAsync(
                _workspacePath,
                trackedDocument.FilePath,
                cancellationToken);

            if (comparison is null)
            {
                return BuildReloadDelta("Reconcile could not access the current workspace session.");
            }

            if (!comparison.DocumentExistsInWorkspace ||
                !comparison.DiskFileExists ||
                !comparison.DiskReadSucceeded)
            {
                return BuildReloadDelta($"Reconcile detected structural drift for {comparison.FilePath}.");
            }

            if (comparison.IsSynchronized)
            {
                _stateTracker.UpsertDocument(
                    filePath: comparison.FilePath,
                    documentKey: comparison.DocumentKey ?? trackedDocument.DocumentKey,
                    workspaceTextHash: comparison.WorkspaceTextHash,
                    diskTextHash: comparison.DiskTextHash,
                    pendingOperationId: trackedDocument.PendingOperationId,
                    lastSynchronizedAtUtc: DateTimeOffset.UtcNow,
                    suppressedUntilUtc: null);
                continue;
            }

            if (!string.Equals(Path.GetExtension(comparison.FilePath), ".cs", StringComparison.OrdinalIgnoreCase))
            {
                return BuildReloadDelta($"Reconcile detected non-C# content drift for {comparison.FilePath}.");
            }

            patchDocuments.Add(new DocumentPatch(
                FilePath: comparison.FilePath,
                ExpectedTextHash: null,
                CurrentWorkspaceTextHash: comparison.WorkspaceTextHash,
                CurrentDiskTextHash: comparison.DiskTextHash));

            if (patchDocuments.Count > ReconcilePatchEscalationThreshold)
            {
                return BuildReloadDelta("Reconcile detected a large divergence set and escalated to solution reload.");
            }
        }

        return new WorkspaceDelta(
            PatchDocuments: patchDocuments.ToImmutableArray(),
            ReloadProjects: ImmutableArray<ProjectReloadRequest>.Empty,
            ReloadSolution: false,
            ReconcileOnly: patchDocuments.Count == 0,
            Origin: ChangeOrigin.Reconcile,
            Reason: patchDocuments.Count == 0
                ? "Reconcile sweep confirmed workspace and disk are synchronized."
                : $"Reconcile sweep prepared {patchDocuments.Count} patch candidate(s).");
    }

    private async ValueTask RefreshAfterReloadAsync(CancellationToken cancellationToken)
    {
        await RebuildSubscriptionAsync(cancellationToken);
        await InitializeTrackedStateAsync(cancellationToken);
        _expectedExternalChangeStore.RemoveExpired(DateTimeOffset.UtcNow);
        _stateTracker.ReplaceExpectedExternalChangeCount(_expectedExternalChangeStore.Count);
    }

    private async ValueTask InitializeTrackedStateAsync(CancellationToken cancellationToken)
    {
        var session = await workspaceAccessor.GetSessionAsync(_workspacePath, cancellationToken);
        if (session is null)
        {
            return;
        }

        workspacePathIndex.Rebuild(session.Solution);
        _stateTracker.ClearDocuments();

        foreach (var project in session.Solution.Projects)
        {
            foreach (var document in project.Documents)
            {
                if (string.IsNullOrWhiteSpace(document.FilePath))
                {
                    continue;
                }

                var comparison = await comparisonService.CompareAsync(
                    _workspacePath,
                    document.FilePath,
                    cancellationToken);
                if (comparison is null)
                {
                    continue;
                }

                _stateTracker.UpsertDocument(
                    filePath: document.FilePath,
                    documentKey: document.Id.ToString(),
                    workspaceTextHash: comparison.WorkspaceTextHash,
                    diskTextHash: comparison.DiskTextHash,
                    pendingOperationId: null,
                    lastSynchronizedAtUtc: DateTimeOffset.UtcNow,
                    suppressedUntilUtc: null);
            }
        }
    }

    private async ValueTask RebuildSubscriptionAsync(CancellationToken cancellationToken)
    {
        IDisposable? previousSubscription;
        CancellationTokenSource? lifetimeCts;

        lock (_gate)
        {
            previousSubscription = _subscription;
            lifetimeCts = _lifetimeCts;

            if (lifetimeCts is null || _pendingDeltas is null)
            {
                return;
            }

            var stream = eventPipeline.Create(
                _workspacePath,
                fileSystemEventSource.Create(_workspacePath),
                roslynWorkspaceEventSource.Create(_workspacePath),
                reconcileTimerEventSource.Create(_workspacePath),
                lifetimeCts.Token);

            _subscription = stream.Subscribe(delta =>
            {
                _stateTracker.MarkEventObserved(DateTimeOffset.UtcNow);
                if (_pendingDeltas.Writer.TryWrite(delta))
                {
                    MarkDeltaQueued();
                }
            });
        }

        previousSubscription?.Dispose();
        await ValueTask.CompletedTask;
        cancellationToken.ThrowIfCancellationRequested();
    }
    private static (WorkspaceDelta Delta, int ConsumedCount) MergePendingDeltas(WorkspaceDelta initialDelta, ChannelReader<WorkspaceDelta> reader)
    {
        var patchDocuments = initialDelta.PatchDocuments.ToBuilder();
        var reloadProjects = initialDelta.ReloadProjects.ToBuilder();
        var reloadSolution = initialDelta.ReloadSolution;
        var reconcileOnly = initialDelta.ReconcileOnly;
        var origin = initialDelta.Origin;
        var reasons = new List<string> { initialDelta.Reason };
        var consumedCount = 1;

        while (reader.TryRead(out var pendingDelta))
        {
            consumedCount++;
            reloadSolution |= pendingDelta.ReloadSolution;
            reconcileOnly &= pendingDelta.ReconcileOnly;

            if (pendingDelta.ReloadProjects.Length > 0)
            {
                reloadProjects.AddRange(pendingDelta.ReloadProjects);
            }

            if (pendingDelta.PatchDocuments.Length > 0)
            {
                patchDocuments.AddRange(pendingDelta.PatchDocuments);
            }

            origin = pendingDelta.Origin;
            if (!string.IsNullOrWhiteSpace(pendingDelta.Reason))
            {
                reasons.Add(pendingDelta.Reason);
            }
        }

        var mergedPatchDocuments = reloadSolution
            ? ImmutableArray<DocumentPatch>.Empty
            : patchDocuments
                .GroupBy(static patch => patch.FilePath, StringComparer.OrdinalIgnoreCase)
                .Select(static group => group.Last())
                .ToImmutableArray();

        var mergedReloadProjects = reloadSolution
            ? ImmutableArray<ProjectReloadRequest>.Empty
            : reloadProjects
                .GroupBy(static reload => reload.ProjectPath, StringComparer.OrdinalIgnoreCase)
                .Select(static group => group.Last())
                .ToImmutableArray();

        var isReconcileOnly = !reloadSolution &&
                              mergedReloadProjects.Length == 0 &&
                              mergedPatchDocuments.Length == 0 &&
                              reconcileOnly;

        return (
            new WorkspaceDelta(
                PatchDocuments: mergedPatchDocuments,
                ReloadProjects: mergedReloadProjects,
                ReloadSolution: reloadSolution,
                ReconcileOnly: isReconcileOnly,
                Origin: origin,
                Reason: string.Join(" | ", reasons.Where(static reason => !string.IsNullOrWhiteSpace(reason)).Distinct(StringComparer.Ordinal))),
            consumedCount);
    }

    private static WorkspaceDelta BuildReloadDelta(string reason)
    {
        return new WorkspaceDelta(
            PatchDocuments: ImmutableArray<DocumentPatch>.Empty,
            ReloadProjects: ImmutableArray<ProjectReloadRequest>.Empty,
            ReloadSolution: true,
            ReconcileOnly: false,
            Origin: ChangeOrigin.Reconcile,
            Reason: reason);
    }

    private static string NormalizePath(string path) => Path.GetFullPath(path);

    private void MarkDeltaQueued()
    {
        Interlocked.Increment(ref _pendingDeltaCount);

        lock (_quiescenceGate)
        {
            if (_quiescenceTcs.Task.IsCompleted)
            {
                _quiescenceTcs = CreatePendingQuiescenceSource();
            }
        }
    }

    private void MarkDeltaApplied(int consumedCount)
    {
        if (Interlocked.Add(ref _pendingDeltaCount, -consumedCount) == 0)
        {
            lock (_quiescenceGate)
            {
                _quiescenceTcs.TrySetResult();
            }
        }
    }

    private static TaskCompletionSource CreateCompletedQuiescenceSource()
    {
        var tcs = CreatePendingQuiescenceSource();
        tcs.SetResult();
        return tcs;
    }

    private static TaskCompletionSource CreatePendingQuiescenceSource() =>
        new(TaskCreationOptions.RunContinuationsAsynchronously);
}
