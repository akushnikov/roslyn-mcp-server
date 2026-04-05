namespace RoslynMcpServer.Abstractions.Infrastructure.Models;

/// <summary>
/// Describes the runtime environment used by the MCP server.
/// </summary>
public sealed record EnvironmentDescriptor(
    string? DotnetSdkVersion,
    bool IsMsBuildAvailable,
    string MsBuildLocatorStatus);
