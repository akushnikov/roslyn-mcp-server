using RoslynMcpServer.Abstractions.Server.Results;

namespace RoslynMcpServer.Abstractions.Server.Services;

/// <summary>
/// Collects raw process metadata shared by the server-info and diagnostic queries.
/// </summary>
public interface IServerInfoQueryDataProvider
{
    /// <summary>Collects a fresh server metadata snapshot.</summary>
    /// <param name="cancellationToken">Cancels snapshot collection.</param>
    /// <returns>The server identity, runtime information and current UTC timestamp.</returns>
    ValueTask<GetServerInfoResult> GetServerInfoAsync(CancellationToken cancellationToken = default);
}
