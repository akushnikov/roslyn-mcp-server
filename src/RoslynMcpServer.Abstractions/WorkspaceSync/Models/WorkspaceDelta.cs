using System.Collections.Immutable;

namespace RoslynMcpServer.Abstractions.WorkspaceSync.Models;

/// <summary>
/// Describes the next set of synchronization actions that should be applied to a workspace.
/// </summary>
public sealed record WorkspaceDelta(
    ImmutableArray<DocumentPatch> PatchDocuments,
    ImmutableArray<ProjectReloadRequest> ReloadProjects,
    bool ReloadSolution,
    bool ReconcileOnly,
    ChangeOrigin Origin,
    string Reason);
