namespace RoslynMcpServer.Infrastructure.Roslyn.Workspace;

/// <summary>
/// Provides internal access to live Roslyn workspace state kept in the solution cache.
/// </summary>
internal interface IRoslynWorkspaceAccessor
{
    /// <summary>
    /// Returns the loaded Roslyn solution for the specified workspace path when available.
    /// </summary>
    ValueTask<RoslynWorkspaceSession?> GetSessionAsync(
        string solutionPath,
        CancellationToken cancellationToken = default);
}
