using RoslynMcpServer.Abstractions.Navigation.Models;

namespace RoslynMcpServer.Abstractions.Navigation.Results;

/// <summary>
/// Represents a compact overview for a resolved type.
/// </summary>
public sealed record GetTypeOverviewResult(
    SymbolDescriptor? Type,
    SymbolDescriptor? BaseType,
    IReadOnlyList<SymbolDescriptor> Interfaces,
    IReadOnlyList<TypeMemberDescriptor> Members,
    string? FailureReason,
    string? Guidance);
