namespace RoslynMcpServer.Abstractions.Navigation.Models;

/// <summary>
/// Describes an attribute applied to a symbol.
/// </summary>
public sealed record SymbolAttributeDescriptor(
    string Name,
    string DisplayName,
    IReadOnlyList<string> ConstructorArguments,
    IReadOnlyList<string> NamedArguments);
