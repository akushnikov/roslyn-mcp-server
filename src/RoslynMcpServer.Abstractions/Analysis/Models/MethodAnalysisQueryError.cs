using RoslynMcpServer.Abstractions.Navigation.Models;

namespace RoslynMcpServer.Abstractions.Analysis.Models;

/// <summary>Preserves resolved context when the MethodAnalysis query cannot complete.</summary>
/// <param name="FailureReason">Transport-safe failure description.</param>
/// <param name="Guidance">Optional recovery guidance for the caller.</param>
/// <param name="Signature">Context resolved before the failure, if available.</param>
public sealed record MethodAnalysisQueryError(
    string FailureReason,
    string? Guidance,
    MethodSignatureDescriptor? Signature = null);
