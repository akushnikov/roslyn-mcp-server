using System.Collections.Immutable;
using Microsoft.Extensions.Logging;
using RoslynMcpServer.Abstractions.WorkspaceSync.Models;

namespace RoslynMcpServer.Infrastructure.Roslyn.WorkspaceSync;

/// <summary>
/// Decides which synchronization action should be taken after a batch of normalized events.
/// </summary>
internal sealed class WorkspaceChangePolicy(ILogger<WorkspaceChangePolicy> logger)
{
    private const int PatchEscalationThreshold = 64;

    /// <summary>
    /// Builds a conservative workspace delta for the supplied event batch.
    /// </summary>
    public ValueTask<WorkspaceDelta> EvaluateAsync(
        string workspacePath,
        IList<WorkspaceEventEnvelope> events,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspacePath);
        ArgumentNullException.ThrowIfNull(events);
        cancellationToken.ThrowIfCancellationRequested();

        logger.LogDebug(
            "Evaluating workspace delta for {WorkspacePath} from {EventCount} events",
            workspacePath,
            events.Count);

        if (events.Count == 0)
        {
            return ValueTask.FromResult(new WorkspaceDelta(
                PatchDocuments: ImmutableArray<DocumentPatch>.Empty,
                ReloadProjects: ImmutableArray<ProjectReloadRequest>.Empty,
                ReloadSolution: false,
                ReconcileOnly: true,
                Origin: ChangeOrigin.Reconcile,
                Reason: "No events were supplied."));
        }

        var normalizedWorkspacePath = NormalizePath(workspacePath);
        var origin = events[^1].Origin;
        var patchDocuments = ImmutableArray.CreateBuilder<DocumentPatch>();
        var reloadProjects = ImmutableArray.CreateBuilder<ProjectReloadRequest>();
        var requiresSolutionReload = false;
        var requiresReconcile = false;

        foreach (var signal in events)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (signal.Kind is WorkspaceFileEventKind.ReconcileRequested)
            {
                requiresReconcile = true;
                continue;
            }

            if (signal.Kind is WorkspaceFileEventKind.Faulted)
            {
                requiresSolutionReload = true;
                continue;
            }

            if (ShouldReloadSolution(signal))
            {
                requiresSolutionReload = true;
                continue;
            }

            if (ShouldReloadProject(signal, normalizedWorkspacePath))
            {
                reloadProjects.Add(new ProjectReloadRequest(signal.FilePath!, signal.Reason));
                continue;
            }

            if (ShouldPatch(signal))
            {
                patchDocuments.Add(new DocumentPatch(
                    FilePath: signal.FilePath!,
                    ExpectedTextHash: signal.ExpectedTextHash,
                    CurrentWorkspaceTextHash: null,
                    CurrentDiskTextHash: null));
                continue;
            }

            requiresReconcile = true;
        }

        return ValueTask.FromResult(new WorkspaceDelta(
            PatchDocuments: patchDocuments
                .GroupBy(static patch => patch.FilePath, StringComparer.OrdinalIgnoreCase)
                .Select(static group => group.Last())
                .ToImmutableArray(),
            ReloadProjects: reloadProjects
                .GroupBy(static reload => reload.ProjectPath, StringComparer.OrdinalIgnoreCase)
                .Select(static group => group.Last())
                .ToImmutableArray(),
            ReloadSolution: requiresSolutionReload || patchDocuments.Count > PatchEscalationThreshold,
            ReconcileOnly: !requiresSolutionReload && patchDocuments.Count == 0 && reloadProjects.Count == 0 && requiresReconcile,
            Origin: origin,
            Reason: BuildReason(
                events.Count,
                requiresSolutionReload || patchDocuments.Count > PatchEscalationThreshold,
                patchDocuments.Count,
                reloadProjects.Count,
                requiresReconcile)));
    }

    private static bool ShouldPatch(WorkspaceEventEnvelope signal)
    {
        if (signal.FilePath is null)
        {
            return false;
        }

        return signal.Kind is WorkspaceFileEventKind.Changed or WorkspaceFileEventKind.WorkspacePatched
            && string.Equals(Path.GetExtension(signal.FilePath), ".cs", StringComparison.OrdinalIgnoreCase)
            && signal.OldFilePath is null;
    }

    private static bool ShouldReloadProject(WorkspaceEventEnvelope signal, string workspacePath)
    {
        if (!string.Equals(Path.GetExtension(workspacePath), ".csproj", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return signal.FilePath is not null &&
               string.Equals(Path.GetExtension(signal.FilePath), ".csproj", StringComparison.OrdinalIgnoreCase);
    }

    private static bool ShouldReloadSolution(WorkspaceEventEnvelope signal)
    {
        if (signal.FilePath is null && signal.OldFilePath is null)
        {
            return false;
        }

        if (signal.Kind is WorkspaceFileEventKind.Renamed or WorkspaceFileEventKind.Created or WorkspaceFileEventKind.Deleted)
        {
            return IsCompileAffectingPath(signal.FilePath) || IsCompileAffectingPath(signal.OldFilePath);
        }

        return IsSolutionCriticalPath(signal.FilePath) || IsSolutionCriticalPath(signal.OldFilePath);
    }

    private static bool IsSolutionCriticalPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        var fileName = Path.GetFileName(path);
        var extension = Path.GetExtension(path);

        return extension.Equals(".sln", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".slnx", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".csproj", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".props", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".targets", StringComparison.OrdinalIgnoreCase) ||
               fileName.Equals("Directory.Build.props", StringComparison.OrdinalIgnoreCase) ||
               fileName.Equals("Directory.Build.targets", StringComparison.OrdinalIgnoreCase) ||
               fileName.Equals("global.json", StringComparison.OrdinalIgnoreCase) ||
               fileName.Equals("nuget.config", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsCompileAffectingPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        var extension = Path.GetExtension(path);
        return extension.Equals(".cs", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".csproj", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".props", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".targets", StringComparison.OrdinalIgnoreCase);
    }

    private static string BuildReason(
        int eventCount,
        bool reloadSolution,
        int patchCount,
        int projectReloadCount,
        bool reconcileOnly)
    {
        if (reloadSolution)
        {
            return $"Escalated {eventCount} coalesced event(s) to solution reload.";
        }

        if (projectReloadCount > 0)
        {
            return $"Prepared {projectReloadCount} project reload request(s) from {eventCount} coalesced event(s).";
        }

        if (patchCount > 0)
        {
            return $"Prepared {patchCount} document patch candidate(s) from {eventCount} coalesced event(s).";
        }

        if (reconcileOnly)
        {
            return $"Prepared reconcile-only delta from {eventCount} coalesced event(s).";
        }

        return $"Observed {eventCount} coalesced event(s) without a stronger delta.";
    }

    private static string NormalizePath(string path) => Path.GetFullPath(path);
}
