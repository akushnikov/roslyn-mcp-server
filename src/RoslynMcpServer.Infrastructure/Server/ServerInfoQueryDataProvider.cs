using RoslynMcpServer.Abstractions.Server.Results;
using RoslynMcpServer.Abstractions.Server.Services;

namespace RoslynMcpServer.Infrastructure.Server;

/// <summary>
/// Collects fresh process metadata using the Application assembly version supplied by composition.
/// </summary>
/// <param name="serverVersion">The server version from the Application assembly.</param>
public sealed class ServerInfoQueryDataProvider(string serverVersion) : IServerInfoQueryDataProvider
{
    /// <inheritdoc />
    public ValueTask<GetServerInfoResult> GetServerInfoAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return ValueTask.FromResult(new GetServerInfoResult(
            Name: "roslyn-mcp-server",
            Version: serverVersion,
            Framework: Environment.Version.ToString(),
            Os: Environment.OSVersion.ToString(),
            UtcNow: DateTimeOffset.UtcNow));
    }
}
