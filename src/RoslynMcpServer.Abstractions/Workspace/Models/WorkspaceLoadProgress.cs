namespace RoslynMcpServer.Abstractions.Workspace.Models;

/// <summary>
/// Represents a coarse-grained progress update for workspace loading.
/// </summary>
public sealed record WorkspaceLoadProgress(
    string Stage,
    string Message,
    int CurrentStep,
    int TotalSteps);
