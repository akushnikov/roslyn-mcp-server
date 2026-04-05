using RoslynMcpServer.Abstractions.Navigation.Models;

namespace RoslynMcpServer.Abstractions.Analysis.Models;

/// <summary>
/// Describes a call site that invokes a target symbol.
/// </summary>
public sealed record CallerDescriptor(
    string? CallingSymbol,
    SymbolLocation Location,
    string? LineText);
