using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using Microsoft.Extensions.Logging;
using RoslynMcpServer.Abstractions.WorkspaceSync.Models;
using RoslynMcpServer.Infrastructure.Roslyn.Workspace;

namespace RoslynMcpServer.Infrastructure.Roslyn.WorkspaceSync;

/// <summary>
/// Applies document text patches to an already loaded Roslyn workspace.
/// </summary>
internal sealed class DocumentPatchService(
    IRoslynWorkspaceAccessor workspaceAccessor,
    WorkspaceDocumentComparisonService comparisonService,
    ILogger<DocumentPatchService> logger)
{
    /// <summary>
    /// Handles the document patches selected for the specified workspace.
    /// </summary>
    public async ValueTask<DocumentPatchBatchResult> HandleAsync(
        string workspacePath,
        IReadOnlyList<DocumentPatch> patches,
        WorkspaceStateTracker stateTracker,
        ExpectedExternalChangeStore expectedExternalChangeStore,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspacePath);
        ArgumentNullException.ThrowIfNull(patches);
        ArgumentNullException.ThrowIfNull(stateTracker);
        ArgumentNullException.ThrowIfNull(expectedExternalChangeStore);
        cancellationToken.ThrowIfCancellationRequested();

        if (patches.Count == 0)
        {
            return new DocumentPatchBatchResult(
                AppliedCount: 0,
                SkippedCount: 0,
                RequiresSolutionReload: false,
                FailureReason: null);
        }

        var session = await workspaceAccessor.GetSessionAsync(workspacePath, cancellationToken);
        if (session is null)
        {
            return new DocumentPatchBatchResult(
                AppliedCount: 0,
                SkippedCount: 0,
                RequiresSolutionReload: true,
                FailureReason: "No loaded Roslyn session was available for the workspace.");
        }

        if (!session.Workspace.CanApplyChange(ApplyChangesKind.ChangeDocument))
        {
            return new DocumentPatchBatchResult(
                AppliedCount: 0,
                SkippedCount: 0,
                RequiresSolutionReload: true,
                FailureReason: "The current workspace cannot apply document text changes.");
        }

        var appliedCount = 0;
        var skippedCount = 0;
        var currentSolution = session.Workspace.CurrentSolution;

        foreach (var patch in patches)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var outcome = await ApplyPatchAsync(
                workspacePath,
                currentSolution,
                patch,
                stateTracker,
                expectedExternalChangeStore,
                cancellationToken);

            if (outcome.RequiresSolutionReload)
            {
                return new DocumentPatchBatchResult(
                    AppliedCount: appliedCount,
                    SkippedCount: skippedCount,
                    RequiresSolutionReload: true,
                    FailureReason: outcome.FailureReason);
            }

            currentSolution = outcome.Solution ?? currentSolution;
            appliedCount += outcome.AppliedCount;
            skippedCount += outcome.SkippedCount;
        }

        return new DocumentPatchBatchResult(
            AppliedCount: appliedCount,
            SkippedCount: skippedCount,
            RequiresSolutionReload: false,
            FailureReason: null);
    }

    private async ValueTask<SinglePatchResult> ApplyPatchAsync(
        string workspacePath,
        Solution solution,
        DocumentPatch patch,
        WorkspaceStateTracker stateTracker,
        ExpectedExternalChangeStore expectedExternalChangeStore,
        CancellationToken cancellationToken)
    {
        var comparison = await comparisonService.CompareAsync(
            workspacePath,
            patch.FilePath,
            cancellationToken);
        if (comparison is null)
        {
            return SinglePatchResult.RequireReload("No loaded Roslyn session was available while comparing workspace and disk content.");
        }

        var normalizedFilePath = comparison.FilePath;
        if (!comparison.DiskFileExists)
        {
            return SinglePatchResult.RequireReload($"Patch target does not exist on disk: {normalizedFilePath}");
        }

        if (!comparison.DocumentExistsInWorkspace || string.IsNullOrWhiteSpace(comparison.DocumentKey))
        {
            return SinglePatchResult.RequireReload($"Patch target is not part of the loaded workspace: {normalizedFilePath}");
        }

        var documentIds = solution.GetDocumentIdsWithFilePath(normalizedFilePath);
        var primaryDocument = documentIds.Length > 0 ? solution.GetDocument(documentIds[0]) : null;
        if (primaryDocument is null)
        {
            return SinglePatchResult.RequireReload($"The primary document could not be resolved for: {normalizedFilePath}");
        }

        if (!comparison.DiskReadSucceeded || comparison.DiskText is null || comparison.DiskTextHash is null)
        {
            return SinglePatchResult.RequireReload($"The file could not be read safely from disk: {normalizedFilePath}");
        }

        var workspaceText = await primaryDocument.GetTextAsync(cancellationToken);
        var workspaceHash = comparison.WorkspaceTextHash ?? TextHashing.Compute(workspaceText.ToString());
        var diskText = comparison.DiskText;
        var diskHash = comparison.DiskTextHash;
        var synchronizedAtUtc = DateTimeOffset.UtcNow;
        stateTracker.TryGetDocument(normalizedFilePath, out var trackedDocument);

        if (comparison.IsSynchronized)
        {
            stateTracker.UpsertDocument(
                filePath: normalizedFilePath,
                documentKey: primaryDocument.Id.ToString(),
                workspaceTextHash: workspaceHash,
                diskTextHash: diskHash,
                pendingOperationId: trackedDocument?.PendingOperationId,
                lastSynchronizedAtUtc: synchronizedAtUtc,
                suppressedUntilUtc: null);

            expectedExternalChangeStore.Remove(normalizedFilePath);
            return SinglePatchResult.Skip(solution);
        }

        var expectedMatch = expectedExternalChangeStore.Match(
            normalizedFilePath,
            diskHash,
            workspaceHash,
            synchronizedAtUtc);

        if (expectedMatch.IsEcho)
        {
            expectedExternalChangeStore.Remove(normalizedFilePath);
            stateTracker.UpsertDocument(
                filePath: normalizedFilePath,
                documentKey: primaryDocument.Id.ToString(),
                workspaceTextHash: diskHash,
                diskTextHash: diskHash,
                pendingOperationId: null,
                lastSynchronizedAtUtc: synchronizedAtUtc,
                suppressedUntilUtc: expectedMatch.Change?.ExpiresAtUtc);
            return SinglePatchResult.Skip(solution);
        }

        if (expectedMatch.HasMatch && trackedDocument?.SuppressedUntilUtc is not null &&
            trackedDocument.SuppressedUntilUtc > synchronizedAtUtc)
        {
            stateTracker.UpsertDocument(
                filePath: normalizedFilePath,
                documentKey: primaryDocument.Id.ToString(),
                workspaceTextHash: workspaceHash,
                diskTextHash: diskHash,
                pendingOperationId: expectedMatch.Change?.OperationId,
                lastSynchronizedAtUtc: trackedDocument.LastSynchronizedAtUtc,
                suppressedUntilUtc: trackedDocument.SuppressedUntilUtc);
        }

        var newText = SourceText.From(diskText, workspaceText.Encoding ?? Encoding.UTF8);
        var nextSolution = solution.WithDocumentText(documentIds, newText, PreservationMode.PreserveValue);
        if (!primaryDocument.Project.Solution.Workspace.TryApplyChanges(nextSolution))
        {
            return SinglePatchResult.RequireReload($"Workspace.TryApplyChanges returned false for: {normalizedFilePath}");
        }

        stateTracker.UpsertDocument(
            filePath: normalizedFilePath,
            documentKey: primaryDocument.Id.ToString(),
            workspaceTextHash: diskHash,
            diskTextHash: diskHash,
            pendingOperationId: null,
            lastSynchronizedAtUtc: synchronizedAtUtc,
            suppressedUntilUtc: null);
        expectedExternalChangeStore.Remove(normalizedFilePath);

        logger.LogInformation(
            "Patched workspace document {FilePath} from disk for workspace {WorkspacePath}",
            normalizedFilePath,
            NormalizePath(solution.FilePath ?? primaryDocument.Project.FilePath ?? normalizedFilePath));

        return SinglePatchResult.Applied(primaryDocument.Project.Solution.Workspace.CurrentSolution);
    }

    private static string NormalizePath(string path) => Path.GetFullPath(path);

    private sealed record SinglePatchResult(
        Solution? Solution,
        int AppliedCount,
        int SkippedCount,
        bool RequiresSolutionReload,
        string? FailureReason)
    {
        public static SinglePatchResult Applied(Solution solution) => new(
            Solution: solution,
            AppliedCount: 1,
            SkippedCount: 0,
            RequiresSolutionReload: false,
            FailureReason: null);

        public static SinglePatchResult Skip(Solution solution) => new(
            Solution: solution,
            AppliedCount: 0,
            SkippedCount: 1,
            RequiresSolutionReload: false,
            FailureReason: null);

        public static SinglePatchResult RequireReload(string reason) => new(
            Solution: null,
            AppliedCount: 0,
            SkippedCount: 0,
            RequiresSolutionReload: true,
            FailureReason: reason);
    }
}
