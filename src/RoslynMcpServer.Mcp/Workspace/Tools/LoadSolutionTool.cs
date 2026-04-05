using System.ComponentModel;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using RoslynMcpServer.Abstractions.Workspace.Models;
using RoslynMcpServer.Abstractions.Workspace.Requests;
using RoslynMcpServer.Abstractions.Workspace.Results;
using RoslynMcpServer.Abstractions.Workspace.Services;

namespace RoslynMcpServer.Mcp.Workspace.Tools;

/// <summary>
/// Loads a solution or project into the server-side workspace cache.
/// </summary>
[McpServerToolType]
internal sealed class LoadSolutionTool(IWorkspaceLoader workspaceLoader)
{
    /// <summary>
    /// Loads the requested solution or project into cache for later read-only operations.
    /// </summary>
    [McpServerTool(Name = "load_solution")]
    [Description("Loads a .sln, .slnx, or .csproj file into the workspace cache.")]
    public ValueTask<LoadSolutionResult> LoadSolution(
        [Description("Absolute path to the .sln, .slnx, or .csproj file to load.")] string solutionPath,
        [Description("When true, forces a full reload even if the solution is already cached.")] bool forceReload = false,
        IProgress<ProgressNotificationValue>? progress = null,
        CancellationToken cancellationToken = default)
        => workspaceLoader.LoadAsync(
            new LoadSolutionRequest(solutionPath, forceReload),
            progress is null ? null : new WorkspaceLoadProgressReporter(progress),
            cancellationToken);

    private sealed class WorkspaceLoadProgressReporter(IProgress<ProgressNotificationValue> progress) : IProgress<WorkspaceLoadProgress>
    {
        public void Report(WorkspaceLoadProgress value)
            => progress.Report(new ProgressNotificationValue
            {
                Progress = value.CurrentStep,
                Total = value.TotalSteps,
                Message = value.Message
            });
    }
}
