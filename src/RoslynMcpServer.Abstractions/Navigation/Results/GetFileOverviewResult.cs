using RoslynMcpServer.Abstractions.Navigation.Models;

namespace RoslynMcpServer.Abstractions.Navigation.Results;

/// <summary>
/// Represents a compact overview for a source file.
/// </summary>
public sealed record GetFileOverviewResult(
    string? FilePath,
    IReadOnlyList<string> Namespaces,
    IReadOnlyList<FileTypeDescriptor> Types,
    string? FailureReason,
    string? Guidance);
