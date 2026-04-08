using RoslynMcpServer.Abstractions.CommandPipeline.Models;

namespace RoslynMcpServer.Abstractions.Workspace.Results;

/// <summary>
/// Successful payload for load-solution command execution.
/// </summary>
public sealed record LoadSolutionCommandResult(
    LoadSolutionResult Result,
    CommandEffects Effects);
