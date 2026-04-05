namespace RoslynMcpServer.Abstractions.Navigation.Models;

/// <summary>
/// Represents a 1-based source location inside a file.
/// </summary>
public sealed record SymbolLocation(
    string? FilePath,
    int Line,
    int Column);
