namespace RoslynMcpServer.Abstractions.Navigation.Models;

/// <summary>
/// Describes a source symbol in a transport-safe shape for MCP responses.
/// </summary>
public sealed record SymbolDescriptor(
    string Name,
    string Kind,
    string DisplayName,
    string? ContainerName,
    SymbolLocation? Definition);
