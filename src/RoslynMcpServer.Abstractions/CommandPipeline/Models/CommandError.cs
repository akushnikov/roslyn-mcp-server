namespace RoslynMcpServer.Abstractions.CommandPipeline.Models;

/// <summary>
/// Canonical command pipeline error payload for transport-safe failures.
/// </summary>
/// <param name="FailureReason">Human-readable failure reason.</param>
/// <param name="Guidance">Optional action guidance for the caller.</param>
public sealed record CommandError(string FailureReason, string? Guidance);
