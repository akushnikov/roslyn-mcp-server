namespace RoslynMcpServer.Abstractions.QueryPipeline.Models;

/// <summary>
/// Canonical query pipeline error payload for transport-safe failures.
/// </summary>
/// <param name="FailureReason">Human-readable failure reason.</param>
/// <param name="Guidance">Optional action guidance for the caller.</param>
public sealed record QueryError(string FailureReason, string? Guidance);
