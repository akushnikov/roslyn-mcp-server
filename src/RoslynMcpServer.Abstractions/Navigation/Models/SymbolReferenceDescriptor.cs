namespace RoslynMcpServer.Abstractions.Navigation.Models;

/// <summary>
/// Describes a single source reference to a symbol.
/// </summary>
public sealed record SymbolReferenceDescriptor(
    SymbolLocation Location,
    string? ContainingSymbol,
    string? LineText);
