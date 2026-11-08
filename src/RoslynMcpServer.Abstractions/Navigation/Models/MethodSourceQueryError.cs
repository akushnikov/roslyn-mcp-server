namespace RoslynMcpServer.Abstractions.Navigation.Models;

/// <summary>Retains a resolved signature when a method-source query cannot return source.</summary>
public sealed record MethodSourceQueryError(
    string FailureReason,
    string? Guidance,
    MethodSignatureDescriptor? Signature = null);
