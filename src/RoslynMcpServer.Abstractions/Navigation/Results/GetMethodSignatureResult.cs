using RoslynMcpServer.Abstractions.Navigation.Models;

namespace RoslynMcpServer.Abstractions.Navigation.Results;

/// <summary>
/// Represents signature details for a resolved callable member.
/// </summary>
public sealed record GetMethodSignatureResult(
    MethodSignatureDescriptor? Signature,
    string? FailureReason,
    string? Guidance);
