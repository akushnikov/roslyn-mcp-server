namespace RoslynMcpServer.Abstractions.Navigation.Models;

/// <summary>
/// Describes a method parameter in a transport-safe shape.
/// </summary>
public sealed record MethodParameterDescriptor(
    string Name,
    string Type,
    string RefKind,
    bool IsOptional,
    string? DefaultValue);
