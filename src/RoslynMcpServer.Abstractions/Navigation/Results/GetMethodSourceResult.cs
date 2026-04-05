using RoslynMcpServer.Abstractions.Navigation.Models;

namespace RoslynMcpServer.Abstractions.Navigation.Results;

/// <summary>
/// Represents source text for a resolved callable member.
/// </summary>
public sealed record GetMethodSourceResult(
    MethodSignatureDescriptor? Signature,
    MethodSourceDescriptor? Method,
    string? FailureReason,
    string? Guidance);
