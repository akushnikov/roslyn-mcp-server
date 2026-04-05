using RoslynMcpServer.Abstractions.Navigation.Models;

namespace RoslynMcpServer.Abstractions.Navigation.Results;

/// <summary>
/// Represents attributes applied to a resolved symbol.
/// </summary>
public sealed record GetSymbolAttributesResult(
    SymbolDescriptor? Symbol,
    IReadOnlyList<SymbolAttributeDescriptor> Attributes,
    string? FailureReason,
    string? Guidance);
