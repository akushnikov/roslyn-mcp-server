namespace RoslynMcpServer.Abstractions.Navigation.Models;

/// <summary>
/// Describes the source span and contents of a callable member.
/// </summary>
public sealed record MethodSourceDescriptor(
    string FilePath,
    SymbolLocation Start,
    SymbolLocation End,
    string Source);
