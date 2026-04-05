namespace RoslynMcpServer.Abstractions.Navigation.Models;

/// <summary>
/// Describes a type member in a transport-safe shape.
/// </summary>
public sealed record TypeMemberDescriptor(
    string Name,
    string Kind,
    string DisplayName,
    string Accessibility,
    bool IsStatic,
    SymbolLocation? Definition);
