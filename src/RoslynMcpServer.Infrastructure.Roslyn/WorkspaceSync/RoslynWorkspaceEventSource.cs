using System.Reactive.Linq;
using Microsoft.Extensions.Logging;
using RoslynMcpServer.Abstractions.WorkspaceSync.Models;
using RoslynMcpServer.Infrastructure.Roslyn.Workspace;
using Microsoft.CodeAnalysis;

namespace RoslynMcpServer.Infrastructure.Roslyn.WorkspaceSync;

/// <summary>
/// Adapts in-process Roslyn workspace mutations into normalized synchronization events.
/// </summary>
internal sealed class RoslynWorkspaceEventSource(
    IRoslynWorkspaceAccessor workspaceAccessor,
    ILogger<RoslynWorkspaceEventSource> logger)
{
    /// <summary>
    /// Creates the observable stream of Roslyn-originated events for the requested workspace.
    /// </summary>
    public IObservable<WorkspaceEventEnvelope> Create(string workspacePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspacePath);

        var normalizedWorkspacePath = NormalizePath(workspacePath);

        return Observable.Defer(() =>
                Observable.FromAsync(ct => workspaceAccessor.GetSessionAsync(normalizedWorkspacePath, ct).AsTask())
                    .SelectMany(session =>
                    {
                        if (session is null)
                        {
                            logger.LogDebug("No loaded Roslyn session found for workspace {WorkspacePath}", normalizedWorkspacePath);
                            return Observable.Empty<WorkspaceEventEnvelope>();
                        }

                        logger.LogDebug("Creating Roslyn workspace event source for workspace {WorkspacePath}", normalizedWorkspacePath);

                        return Observable.Create<WorkspaceEventEnvelope>(observer =>
                            session.Workspace.RegisterWorkspaceChangedHandler(
                                args =>
                                {
                                    var envelope = MapEvent(normalizedWorkspacePath, args);
                                    if (envelope is not null)
                                    {
                                        observer.OnNext(envelope);
                                    }
                                },
                                options: null));
                    }))
            .Publish()
            .RefCount();
    }

    private static WorkspaceEventEnvelope? MapEvent(string workspacePath, WorkspaceChangeEventArgs args)
    {
        var filePath = ResolveFilePath(args.NewSolution, args.DocumentId)
            ?? ResolveFilePath(args.OldSolution, args.DocumentId)
            ?? ResolveProjectPath(args.NewSolution, args.ProjectId)
            ?? ResolveProjectPath(args.OldSolution, args.ProjectId);

        var kind = args.Kind switch
        {
            WorkspaceChangeKind.DocumentChanged or
            WorkspaceChangeKind.AdditionalDocumentChanged or
            WorkspaceChangeKind.AnalyzerConfigDocumentChanged => WorkspaceFileEventKind.WorkspacePatched,
            WorkspaceChangeKind.DocumentAdded or
            WorkspaceChangeKind.AdditionalDocumentAdded or
            WorkspaceChangeKind.AnalyzerConfigDocumentAdded or
            WorkspaceChangeKind.ProjectAdded => WorkspaceFileEventKind.Created,
            WorkspaceChangeKind.DocumentRemoved or
            WorkspaceChangeKind.AdditionalDocumentRemoved or
            WorkspaceChangeKind.AnalyzerConfigDocumentRemoved or
            WorkspaceChangeKind.ProjectRemoved => WorkspaceFileEventKind.Deleted,
            WorkspaceChangeKind.DocumentReloaded or
            WorkspaceChangeKind.ProjectReloaded or
            WorkspaceChangeKind.SolutionReloaded => WorkspaceFileEventKind.WorkspacePatched,
            WorkspaceChangeKind.SolutionAdded => WorkspaceFileEventKind.Created,
            WorkspaceChangeKind.SolutionRemoved => WorkspaceFileEventKind.Deleted,
            _ => WorkspaceFileEventKind.WorkspacePatched
        };

        return new WorkspaceEventEnvelope(
            WorkspacePath: workspacePath,
            FilePath: NormalizePathOrNull(filePath),
            OldFilePath: null,
            OccurredAtUtc: DateTimeOffset.UtcNow,
            Origin: ChangeOrigin.RoslynInternal,
            Kind: kind,
            Reason: args.Kind.ToString());
    }

    private static string? ResolveFilePath(Solution? solution, DocumentId? documentId)
    {
        if (solution is null || documentId is null)
        {
            return null;
        }

        return solution.GetDocument(documentId)?.FilePath
            ?? solution.GetAdditionalDocument(documentId)?.FilePath
            ?? solution.GetAnalyzerConfigDocument(documentId)?.FilePath;
    }

    private static string? ResolveProjectPath(Solution? solution, ProjectId? projectId)
    {
        if (solution is null || projectId is null)
        {
            return null;
        }

        return solution.GetProject(projectId)?.FilePath;
    }

    private static string NormalizePath(string path) => Path.GetFullPath(path);

    private static string? NormalizePathOrNull(string? path) =>
        string.IsNullOrWhiteSpace(path)
            ? null
            : NormalizePath(path);
}
