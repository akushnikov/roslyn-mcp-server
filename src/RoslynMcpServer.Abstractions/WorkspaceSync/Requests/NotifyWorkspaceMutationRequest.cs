using System.Collections.Immutable;
using RoslynMcpServer.Abstractions.WorkspaceSync.Models;

namespace RoslynMcpServer.Abstractions.WorkspaceSync.Requests;

/// <summary>
/// Reports a Roslyn-originated mutation so watcher echo can be recognized after the change reaches disk.
/// </summary>
public sealed record NotifyWorkspaceMutationRequest(
    string WorkspacePath,
    string OperationId,
    ImmutableArray<ExpectedExternalChange> ExpectedExternalChanges);
