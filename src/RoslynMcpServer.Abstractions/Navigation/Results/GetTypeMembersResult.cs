using RoslynMcpServer.Abstractions.Navigation.Models;

namespace RoslynMcpServer.Abstractions.Navigation.Results;

/// <summary>
/// Represents the members exposed by a resolved type.
/// </summary>
public sealed record GetTypeMembersResult(
    SymbolDescriptor? Type,
    IReadOnlyList<TypeMemberDescriptor> Members,
    string? FailureReason,
    string? Guidance);
