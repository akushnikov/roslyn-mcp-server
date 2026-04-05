using RoslynMcpServer.Abstractions.Server.Results;

namespace RoslynMcpServer.Abstractions.Server.Services;

/// <summary>
/// Defines a boundary for retrieving descriptive metadata about the running server instance.
/// </summary>
public interface IServerInfoService
{
    /// <summary>
    /// Returns a runtime snapshot for the current server instance.
    /// </summary>
    ValueTask<GetServerInfoResult> GetServerInfoAsync(CancellationToken cancellationToken = default);
}
