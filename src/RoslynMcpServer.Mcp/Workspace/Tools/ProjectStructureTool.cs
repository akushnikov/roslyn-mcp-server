using System.ComponentModel;
using ModelContextProtocol.Server;
using RoslynMcpServer.Abstractions.Workspace.Requests;
using RoslynMcpServer.Abstractions.Workspace.Results;
using RoslynMcpServer.Abstractions.Workspace.Services;

namespace RoslynMcpServer.Mcp.Workspace.Tools;

/// <summary>
/// Returns project and optional document structure for a loaded solution.
/// </summary>
[McpServerToolType]
internal sealed class ProjectStructureTool(IProjectStructureService projectStructureService)
{
    /// <summary>
    /// Returns projects and optional documents for a previously loaded solution.
    /// </summary>
    [McpServerTool(Name = "get_project_structure")]
    [Description("Returns projects and optional source documents for a previously loaded solution.")]
    public ValueTask<GetProjectStructureResult> GetProjectStructure(
        [Description("Absolute path to a previously loaded .sln, .slnx, or .csproj file.")] string solutionPath,
        [Description("When true, includes source document descriptors for each project.")] bool includeDocuments = false,
        CancellationToken cancellationToken = default)
        => projectStructureService.GetProjectStructureAsync(
            new GetProjectStructureRequest(solutionPath, includeDocuments),
            cancellationToken);
}
