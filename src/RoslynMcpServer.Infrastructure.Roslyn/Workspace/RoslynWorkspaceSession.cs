using Microsoft.CodeAnalysis;
using RoslynMcpServer.Abstractions.Workspace.Models;

namespace RoslynMcpServer.Infrastructure.Roslyn.Workspace;

/// <summary>
/// Combines the transport-safe snapshot with the live Roslyn solution for internal use.
/// </summary>
internal sealed record RoslynWorkspaceSession(
    LoadedWorkspaceSnapshot Snapshot,
    Microsoft.CodeAnalysis.Workspace Workspace,
    Solution Solution);
