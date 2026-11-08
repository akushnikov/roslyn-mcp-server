namespace RoslynMcpServer.Abstractions.Navigation.Models;

/// <summary>Retains the document path available at the stage where a navigation query failed.</summary>
public sealed record DocumentQueryError(
    string FailureReason,
    string? Guidance,
    string? FilePath = null);
