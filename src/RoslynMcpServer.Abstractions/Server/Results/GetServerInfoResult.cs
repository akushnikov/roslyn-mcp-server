namespace RoslynMcpServer.Abstractions.Server.Results;

/// <summary>
/// Represents a runtime snapshot describing the current MCP server instance.
/// </summary>
public sealed record GetServerInfoResult(
    string Name,
    string Version,
    string Framework,
    string Os,
    DateTimeOffset UtcNow);
