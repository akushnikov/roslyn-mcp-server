using System.Reactive.Disposables;
using System.Reactive.Linq;
using Microsoft.Extensions.Logging;
using RoslynMcpServer.Infrastructure.Roslyn.Workspace;
using RoslynMcpServer.Abstractions.WorkspaceSync.Models;

namespace RoslynMcpServer.Infrastructure.Roslyn.WorkspaceSync;

/// <summary>
/// Adapts file system watcher notifications into normalized workspace synchronization events.
/// </summary>
internal sealed class FileSystemWorkspaceEventSource(
    IRoslynWorkspaceAccessor workspaceAccessor,
    ILogger<FileSystemWorkspaceEventSource> logger)
{
    /// <summary>
    /// Creates the observable stream of file system events for the requested workspace.
    /// </summary>
    public IObservable<WorkspaceEventEnvelope> Create(string workspacePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspacePath);

        var normalizedWorkspacePath = NormalizePath(workspacePath);

        return Observable.Defer(() =>
                Observable.FromAsync(ct => workspaceAccessor.GetSessionAsync(normalizedWorkspacePath, ct).AsTask())
                    .SelectMany(session => ObserveDirectories(
                        normalizedWorkspacePath,
                        GetWatchDirectories(normalizedWorkspacePath, session))))
            .Publish()
            .RefCount();
    }

    private IObservable<WorkspaceEventEnvelope> ObserveDirectories(
        string workspacePath,
        IReadOnlyList<string> directories)
    {
        if (directories.Count == 0)
        {
            return Observable.Empty<WorkspaceEventEnvelope>();
        }

        logger.LogDebug(
            "Creating file system watchers for workspace {WorkspacePath} across {DirectoryCount} directories",
            workspacePath,
            directories.Count);

        return Observable.Merge(directories.Select(directory => ObserveDirectory(workspacePath, directory)));
    }

    private static IObservable<WorkspaceEventEnvelope> ObserveDirectory(string workspacePath, string directoryPath)
    {
        return Observable.Create<WorkspaceEventEnvelope>(observer =>
        {
            if (!Directory.Exists(directoryPath))
            {
                observer.OnNext(CreateEvent(
                    workspacePath,
                    filePath: directoryPath,
                    oldFilePath: null,
                    kind: WorkspaceFileEventKind.Faulted,
                    reason: "Watcher directory does not exist."));
                observer.OnCompleted();
                return Disposable.Empty;
            }

            var watcher = new FileSystemWatcher(directoryPath)
            {
                IncludeSubdirectories = true,
                NotifyFilter = NotifyFilters.FileName |
                               NotifyFilters.DirectoryName |
                               NotifyFilters.LastWrite |
                               NotifyFilters.CreationTime |
                               NotifyFilters.Size
            };

            FileSystemEventHandler onChanged = (_, args) =>
                observer.OnNext(CreateEvent(workspacePath, args.FullPath, null, WorkspaceFileEventKind.Changed));
            FileSystemEventHandler onCreated = (_, args) =>
                observer.OnNext(CreateEvent(workspacePath, args.FullPath, null, WorkspaceFileEventKind.Created));
            FileSystemEventHandler onDeleted = (_, args) =>
                observer.OnNext(CreateEvent(workspacePath, args.FullPath, null, WorkspaceFileEventKind.Deleted));
            RenamedEventHandler onRenamed = (_, args) =>
                observer.OnNext(CreateEvent(workspacePath, args.FullPath, args.OldFullPath, WorkspaceFileEventKind.Renamed));
            ErrorEventHandler onError = (_, args) =>
                observer.OnNext(CreateEvent(
                    workspacePath,
                    directoryPath,
                    null,
                    WorkspaceFileEventKind.Faulted,
                    reason: args.GetException().Message));

            watcher.Changed += onChanged;
            watcher.Created += onCreated;
            watcher.Deleted += onDeleted;
            watcher.Renamed += onRenamed;
            watcher.Error += onError;
            watcher.EnableRaisingEvents = true;

            return Disposable.Create(() =>
            {
                watcher.EnableRaisingEvents = false;
                watcher.Changed -= onChanged;
                watcher.Created -= onCreated;
                watcher.Deleted -= onDeleted;
                watcher.Renamed -= onRenamed;
                watcher.Error -= onError;
                watcher.Dispose();
            });
        });
    }

    private static IReadOnlyList<string> GetWatchDirectories(
        string workspacePath,
        RoslynWorkspaceSession? session)
    {
        var directories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        AddDirectoryIfPresent(directories, Path.GetDirectoryName(workspacePath));

        if (session is null)
        {
            return directories.ToArray();
        }

        foreach (var project in session.Solution.Projects)
        {
            AddDirectoryIfPresent(directories, Path.GetDirectoryName(project.FilePath));

            foreach (var document in project.Documents)
            {
                AddDirectoryIfPresent(directories, Path.GetDirectoryName(document.FilePath));
            }
        }

        return directories.ToArray();
    }

    private static void AddDirectoryIfPresent(HashSet<string> directories, string? directoryPath)
    {
        if (string.IsNullOrWhiteSpace(directoryPath))
        {
            return;
        }

        directories.Add(NormalizePath(directoryPath));
    }

    private static WorkspaceEventEnvelope CreateEvent(
        string workspacePath,
        string? filePath,
        string? oldFilePath,
        WorkspaceFileEventKind kind,
        string? reason = null)
    {
        return new WorkspaceEventEnvelope(
            WorkspacePath: workspacePath,
            FilePath: NormalizePathOrNull(filePath),
            OldFilePath: NormalizePathOrNull(oldFilePath),
            OccurredAtUtc: DateTimeOffset.UtcNow,
            Origin: ChangeOrigin.FileSystem,
            Kind: kind,
            Reason: reason);
    }

    private static string NormalizePath(string path) => Path.GetFullPath(path);

    private static string? NormalizePathOrNull(string? path) =>
        string.IsNullOrWhiteSpace(path)
            ? null
            : NormalizePath(path);
}
