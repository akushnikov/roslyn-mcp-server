namespace RoslynMcpServer.Abstractions.Workspace.Models;

/// <summary>
/// Describes a loaded Roslyn workspace in a transport-safe form.
/// </summary>
public sealed record WorkspaceDescriptor(
    string SolutionPath,
    string SolutionName,
    DateTimeOffset LoadedAtUtc,
    int ProjectCount,
    int DocumentCount);
