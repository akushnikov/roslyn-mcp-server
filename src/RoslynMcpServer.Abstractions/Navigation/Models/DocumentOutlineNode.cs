namespace RoslynMcpServer.Abstractions.Navigation.Models;

/// <summary>
/// Describes a declaration inside a source document outline.
/// </summary>
public sealed record DocumentOutlineNode(
    string Name,
    string Kind,
    SymbolLocation Start,
    SymbolLocation End,
    IReadOnlyList<DocumentOutlineNode> Children);
