namespace RoslynMcpServer.Abstractions.WorkspaceSync.Models;

/// <summary>
/// Records an external file change that is expected to appear after an in-process Roslyn mutation.
/// </summary>
public sealed record ExpectedExternalChange(
    string FilePath,
    string? ExpectedTextHash,
    string OperationId,
    DateTimeOffset ExpiresAtUtc);
