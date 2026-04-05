using RoslynMcpServer.Abstractions.Server.Results;
using RoslynMcpServer.Abstractions.Server.Services;

namespace RoslynMcpServer.Application.Server;

/// <summary>
/// Provides process-level metadata about the running MCP server instance.
/// </summary>
public sealed class ServerInfoService : IServerInfoService
{
    /// <summary>
    /// Creates a runtime snapshot describing the current server process.
    /// </summary>
    public ValueTask<GetServerInfoResult> GetServerInfoAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var version = typeof(ServerInfoService).Assembly.GetName().Version?.ToString() ?? "0.1.0";

        return ValueTask.FromResult(new GetServerInfoResult(
            Name: "roslyn-mcp-server",
            Version: version,
            Framework: Environment.Version.ToString(),
            Os: Environment.OSVersion.ToString(),
            UtcNow: DateTimeOffset.UtcNow));
    }
}
