namespace RoslynMcpServer.Abstractions.Navigation.Models;

/// <summary>
/// Describes a callable member signature in a transport-safe shape.
/// </summary>
public sealed record MethodSignatureDescriptor(
    string Name,
    string Kind,
    string DisplayName,
    string Accessibility,
    bool IsStatic,
    bool IsAsync,
    string? ReturnType,
    string? ContainingType,
    IReadOnlyList<MethodParameterDescriptor> Parameters,
    SymbolLocation? Definition);
