using RoslynMcpServer.Abstractions.Server.Results;

namespace RoslynMcpServer.Abstractions.Server.Services;

/// <summary>
/// Retrieves the current server snapshot through the query pipeline.
/// </summary>
public interface IServerInfoQueryService
{
    /// <summary>Returns descriptive metadata for the running server instance.</summary>
    /// <param name="cancellationToken">Cancels snapshot collection.</param>
    /// <returns>The current server metadata snapshot.</returns>
    ValueTask<GetServerInfoResult> GetServerInfoAsync(CancellationToken cancellationToken = default);
}
