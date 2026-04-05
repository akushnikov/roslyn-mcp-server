using System.ComponentModel;
using ModelContextProtocol.Server;
using RoslynMcpServer.Abstractions.Server.Results;
using RoslynMcpServer.Abstractions.Server.Services;

namespace RoslynMcpServer.Mcp.Infrastructure.Tools;

/// <summary>
/// Exposes basic runtime metadata about the current MCP server instance.
/// </summary>
/// <remarks>
/// Use this tool to verify that the server is reachable and to inspect the current
/// process environment before invoking heavier Roslyn-based tools.
/// </remarks>
[McpServerToolType]
internal sealed class ServerInfoTool(IServerInfoService serverInfoService)
{
    /// <summary>
    /// Returns descriptive runtime information about the current server process.
    /// </summary>
    /// <param name="cancellationToken">Cancels the tool invocation.</param>
    /// <returns>
    /// A contract result containing server identity, framework, operating system,
    /// process id, and current UTC timestamp.
    /// </returns>
    [McpServerTool(Name = "server_info"), Description("Returns runtime information about the MCP server instance.")]
    public ValueTask<GetServerInfoResult> GetServerInfo(CancellationToken cancellationToken) =>
        serverInfoService.GetServerInfoAsync(cancellationToken);
}
