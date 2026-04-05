using RoslynMcpServer.Abstractions.WorkspaceSync.Models;

namespace RoslynMcpServer.Infrastructure.Roslyn.WorkspaceSync;

/// <summary>
/// Represents the outcome of matching actual disk content against an expected external change.
/// </summary>
internal sealed record ExpectedExternalChangeMatchResult(
    bool HasMatch,
    bool IsEcho,
    ExpectedExternalChange? Change);
