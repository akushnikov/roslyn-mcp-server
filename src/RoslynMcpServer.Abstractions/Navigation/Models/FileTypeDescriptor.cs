namespace RoslynMcpServer.Abstractions.Navigation.Models;

/// <summary>
/// Describes a top-level or nested type discovered in a source file overview.
/// </summary>
public sealed record FileTypeDescriptor(
    string Name,
    string Kind,
    SymbolLocation Start,
    int MemberCount);
