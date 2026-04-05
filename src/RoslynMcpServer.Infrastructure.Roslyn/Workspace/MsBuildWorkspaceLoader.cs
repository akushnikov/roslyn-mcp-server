using System.Collections.Concurrent;
using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;
using Microsoft.Extensions.Logging;
using RoslynMcpServer.Abstractions.Infrastructure.Models;
using RoslynMcpServer.Abstractions.Workspace.Models;
using RoslynMcpServer.Abstractions.Workspace.Requests;
using RoslynMcpServer.Abstractions.Workspace.Results;
using RoslynMcpServer.Abstractions.Workspace.Services;
using RoslynMcpServer.Abstractions.WorkspaceSync.Requests;
using RoslynMcpServer.Abstractions.WorkspaceSync.Services;

namespace RoslynMcpServer.Infrastructure.Roslyn.Workspace;

/// <summary>
/// Loads SDK-style projects and solutions into an in-memory cache using MSBuildWorkspace.
/// </summary>
internal sealed class MsBuildWorkspaceLoader(
    ILogger<MsBuildWorkspaceLoader> logger,
    IWorkspaceSyncService workspaceSyncService) : IWorkspaceLoader, IWorkspaceCache, IWorkspaceProbeService, IRoslynWorkspaceAccessor
{
    private static readonly object RegistrationLock = new();
    private static bool _registered;
    private readonly ConcurrentDictionary<string, CacheEntry> _entries = new(StringComparer.OrdinalIgnoreCase);

    public ValueTask<LoadedWorkspaceSnapshot?> GetAsync(
        string solutionPath,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var key = NormalizePath(solutionPath);
        return ValueTask.FromResult(
            _entries.TryGetValue(key, out var entry)
                ? WorkspaceSnapshotFactory.CreateSnapshot(entry.Workspace.CurrentSolution, key, entry.LoadedAtUtc)
                : null);
    }

    public ValueTask<RoslynWorkspaceSession?> GetSessionAsync(
        string solutionPath,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var key = NormalizePath(solutionPath);
        return ValueTask.FromResult(
            _entries.TryGetValue(key, out var entry)
                ? new RoslynWorkspaceSession(
                    WorkspaceSnapshotFactory.CreateSnapshot(entry.Workspace.CurrentSolution, key, entry.LoadedAtUtc),
                    entry.Workspace,
                    entry.Workspace.CurrentSolution)
                : null);
    }

    public ValueTask<IReadOnlyList<LoadedWorkspaceSnapshot>> ListAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult<IReadOnlyList<LoadedWorkspaceSnapshot>>(
            _entries.Values
                .Select(static entry => WorkspaceSnapshotFactory.CreateSnapshot(
                    entry.Workspace.CurrentSolution,
                    entry.WorkspacePath,
                    entry.LoadedAtUtc))
                .OrderBy(static snapshot => snapshot.Workspace.SolutionPath, StringComparer.OrdinalIgnoreCase)
                .ToArray());
    }

    public async ValueTask<LoadSolutionResult> LoadAsync(
        LoadSolutionRequest request,
        IProgress<WorkspaceLoadProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(request.SolutionPath))
        {
            return Failure(
                failureReason: "A solutionPath is required.",
                guidance: "Pass an absolute path to a .sln, .slnx, or .csproj file.",
                code: "PathNotFound",
                path: request.SolutionPath);
        }

        var normalizedPath = NormalizePath(request.SolutionPath);
        ReportProgress(progress, "validate-path", $"Validating workspace path: {normalizedPath}", 1, 6);

        if (_entries.TryGetValue(normalizedPath, out var cachedEntry) && !request.ForceReload)
        {
            await workspaceSyncService.EnsureStartedAsync(
                new EnsureWorkspaceSyncRequest(normalizedPath),
                cancellationToken);
            var currentSnapshot = WorkspaceSnapshotFactory.CreateSnapshot(
                cachedEntry.Workspace.CurrentSolution,
                normalizedPath,
                cachedEntry.LoadedAtUtc);
            ReportProgress(progress, "cache-hit", $"Workspace already loaded: {normalizedPath}", 6, 6);
            return new LoadSolutionResult(
                Workspace: currentSnapshot.Workspace,
                WasAlreadyLoaded: true,
                WasReloaded: false,
                Diagnostics: Array.Empty<WorkspaceOperationDiagnostic>(),
                FailureReason: null,
                Guidance: null);
        }

        if (request.ForceReload)
        {
            ReportProgress(progress, "force-reload", $"Removing cached workspace before reload: {normalizedPath}", 2, 6);
            Remove(normalizedPath);
        }

        if (!File.Exists(normalizedPath))
        {
            return Failure(
                failureReason: "The requested solution path does not exist.",
                guidance: "Pass an absolute path to an existing .sln, .slnx, or .csproj file.",
                code: "PathNotFound",
                path: normalizedPath);
        }

        var extension = Path.GetExtension(normalizedPath);
        if (!IsSupportedWorkspacePath(extension))
        {
            return Failure(
                failureReason: "The requested path is not a supported workspace file.",
                guidance: "Only .sln, .slnx, and .csproj files are supported by this tool.",
                code: "UnsupportedWorkspacePath",
                path: normalizedPath);
        }

        try
        {
            ReportProgress(progress, "register-msbuild", "Registering MSBuild services.", 2, 6);
            EnsureMsBuildRegistered();

            var collector = new WorkspaceDiagnosticCollector();
            var workspace = MSBuildWorkspace.Create();
            collector.Attach(workspace);

            var loadedAtUtc = DateTimeOffset.UtcNow;
            ReportProgress(progress, "open-workspace", $"Opening workspace: {normalizedPath}", 3, 6);
            var solution = await OpenAsync(workspace, normalizedPath, extension, cancellationToken);
            ReportProgress(progress, "build-snapshot", "Building workspace snapshot.", 4, 6);
            var snapshot = WorkspaceSnapshotFactory.CreateSnapshot(solution, normalizedPath, loadedAtUtc);

            ReportProgress(progress, "cache-workspace", "Caching loaded workspace.", 5, 6);
            Store(normalizedPath, loadedAtUtc, workspace);
            await workspaceSyncService.EnsureStartedAsync(
                new EnsureWorkspaceSyncRequest(normalizedPath),
                cancellationToken);
            collector.Detach(workspace);

            ReportProgress(progress, "completed", $"Workspace loaded: {snapshot.Workspace.SolutionName}", 6, 6);
            return new LoadSolutionResult(
                Workspace: snapshot.Workspace,
                WasAlreadyLoaded: false,
                WasReloaded: request.ForceReload,
                Diagnostics: collector.Diagnostics,
                FailureReason: null,
                Guidance: null);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Failed to load workspace for {SolutionPath}", normalizedPath);

            return new LoadSolutionResult(
                Workspace: null,
                WasAlreadyLoaded: false,
                WasReloaded: request.ForceReload,
                Diagnostics:
                [
                    new WorkspaceOperationDiagnostic(
                        WorkspaceDiagnosticSeverity.Error,
                        "WorkspaceLoadFailed",
                        exception.Message,
                        normalizedPath)
                ],
                FailureReason: "The requested workspace could not be loaded.",
                Guidance: "Run diagnose with verbose=true to inspect environment and workspace load details.");
        }
    }

    private static void ReportProgress(
        IProgress<WorkspaceLoadProgress>? progress,
        string stage,
        string message,
        int currentStep,
        int totalSteps)
    {
        progress?.Report(new WorkspaceLoadProgress(stage, message, currentStep, totalSteps));
    }

    public async ValueTask<WorkspaceHealthDescriptor> ProbeAsync(
        string solutionPath,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(solutionPath))
        {
            return new WorkspaceHealthDescriptor(
                SolutionPath: null,
                CanBeLoaded: false,
                ProjectCount: 0,
                DocumentCount: 0,
                Diagnostics:
                [
                    new WorkspaceOperationDiagnostic(
                        WorkspaceDiagnosticSeverity.Error,
                        "PathNotFound",
                        "A solutionPath is required.",
                        solutionPath)
                ]);
        }

        var normalizedPath = NormalizePath(solutionPath);
        if (!File.Exists(normalizedPath))
        {
            return new WorkspaceHealthDescriptor(
                SolutionPath: normalizedPath,
                CanBeLoaded: false,
                ProjectCount: 0,
                DocumentCount: 0,
                Diagnostics:
                [
                    new WorkspaceOperationDiagnostic(
                        WorkspaceDiagnosticSeverity.Error,
                        "PathNotFound",
                        "The requested solution path does not exist.",
                        normalizedPath)
                ]);
        }

        var extension = Path.GetExtension(normalizedPath);
        if (!IsSupportedWorkspacePath(extension))
        {
            return new WorkspaceHealthDescriptor(
                SolutionPath: normalizedPath,
                CanBeLoaded: false,
                ProjectCount: 0,
                DocumentCount: 0,
                Diagnostics:
                [
                    new WorkspaceOperationDiagnostic(
                        WorkspaceDiagnosticSeverity.Error,
                        "UnsupportedWorkspacePath",
                        "Only .sln, .slnx, and .csproj files are supported.",
                        normalizedPath)
                ]);
        }

        try
        {
            EnsureMsBuildRegistered();

            var collector = new WorkspaceDiagnosticCollector();
            using var workspace = MSBuildWorkspace.Create();
            collector.Attach(workspace);

            var solution = await OpenAsync(workspace, normalizedPath, extension, cancellationToken);
            var snapshot = WorkspaceSnapshotFactory.CreateSnapshot(solution, normalizedPath, DateTimeOffset.UtcNow);

            return new WorkspaceHealthDescriptor(
                SolutionPath: normalizedPath,
                CanBeLoaded: true,
                ProjectCount: snapshot.Workspace.ProjectCount,
                DocumentCount: snapshot.Workspace.DocumentCount,
                Diagnostics: collector.Diagnostics);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Failed to probe workspace for {SolutionPath}", normalizedPath);

            return new WorkspaceHealthDescriptor(
                SolutionPath: normalizedPath,
                CanBeLoaded: false,
                ProjectCount: 0,
                DocumentCount: 0,
                Diagnostics:
                [
                    new WorkspaceOperationDiagnostic(
                        WorkspaceDiagnosticSeverity.Error,
                        "WorkspaceLoadFailed",
                        exception.Message,
                        normalizedPath)
                ]);
        }
    }

    private static bool IsSupportedWorkspacePath(string extension) =>
        extension.Equals(".sln", StringComparison.OrdinalIgnoreCase) ||
        extension.Equals(".slnx", StringComparison.OrdinalIgnoreCase) ||
        extension.Equals(".csproj", StringComparison.OrdinalIgnoreCase);

    private bool Remove(string solutionPath)
    {
        var key = NormalizePath(solutionPath);
        if (!_entries.TryRemove(key, out var entry))
        {
            return false;
        }

        entry.Dispose();
        return true;
    }

    private void Store(string solutionPath, DateTimeOffset loadedAtUtc, Microsoft.CodeAnalysis.Workspace workspace)
    {
        var key = NormalizePath(solutionPath);
        var entry = new CacheEntry(key, loadedAtUtc, workspace);

        if (_entries.TryGetValue(key, out var existing))
        {
            existing.Dispose();
        }

        _entries[key] = entry;
    }

    private static string NormalizePath(string path) => Path.GetFullPath(path);

    private static async Task<Solution> OpenAsync(
        MSBuildWorkspace workspace,
        string normalizedPath,
        string extension,
        CancellationToken cancellationToken)
    {
        if (extension.Equals(".csproj", StringComparison.OrdinalIgnoreCase))
        {
            var project = await workspace.OpenProjectAsync(normalizedPath, cancellationToken: cancellationToken);
            return project.Solution;
        }

        return await workspace.OpenSolutionAsync(normalizedPath, cancellationToken: cancellationToken);
    }

    private static void EnsureMsBuildRegistered()
    {
        lock (RegistrationLock)
        {
            if (_registered || MSBuildLocator.IsRegistered)
            {
                _registered = true;
                return;
            }

            var instance = MSBuildLocator.QueryVisualStudioInstances().OrderByDescending(static candidate => candidate.Version).FirstOrDefault();
            if (instance is null)
            {
                throw new InvalidOperationException("MSBuildLocator could not find a registered MSBuild instance.");
            }

            MSBuildLocator.RegisterInstance(instance);
            _registered = true;
        }
    }

    private static LoadSolutionResult Failure(
        string failureReason,
        string guidance,
        string code,
        string? path)
        => new(
            Workspace: null,
            WasAlreadyLoaded: false,
            WasReloaded: false,
            Diagnostics:
            [
                new WorkspaceOperationDiagnostic(
                    WorkspaceDiagnosticSeverity.Error,
                    code,
                    failureReason,
                    path)
            ],
            FailureReason: failureReason,
            Guidance: guidance);

    private sealed class WorkspaceDiagnosticCollector
    {
        private readonly List<WorkspaceOperationDiagnostic> _diagnostics = [];
        private IDisposable? _registration;

        public IReadOnlyList<WorkspaceOperationDiagnostic> Diagnostics => _diagnostics.ToArray();

        public void Attach(MSBuildWorkspace workspace)
        {
            _registration = workspace.RegisterWorkspaceFailedHandler(args =>
            {
                var severity = args.Diagnostic.Kind == WorkspaceDiagnosticKind.Failure
                    ? WorkspaceDiagnosticSeverity.Error
                    : WorkspaceDiagnosticSeverity.Warning;

                _diagnostics.Add(new WorkspaceOperationDiagnostic(
                    severity,
                    args.Diagnostic.Kind.ToString(),
                    args.Diagnostic.Message,
                    null));
            });
        }

        public void Detach(MSBuildWorkspace workspace)
        {
            _registration?.Dispose();
            _registration = null;
        }
    }

    private sealed class CacheEntry(
        string workspacePath,
        DateTimeOffset loadedAtUtc,
        Microsoft.CodeAnalysis.Workspace workspace) : IDisposable
    {
        public string WorkspacePath { get; } = workspacePath;

        public DateTimeOffset LoadedAtUtc { get; } = loadedAtUtc;

        public Microsoft.CodeAnalysis.Workspace Workspace { get; } = workspace;

        public void Dispose() => Workspace.Dispose();
    }
}
