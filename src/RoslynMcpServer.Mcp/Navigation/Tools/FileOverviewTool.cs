using System.ComponentModel;
using ModelContextProtocol.Server;
using RoslynMcpServer.Abstractions.Navigation.Requests;
using RoslynMcpServer.Abstractions.Navigation.Results;
using RoslynMcpServer.Abstractions.Navigation.Services;

namespace RoslynMcpServer.Mcp.Navigation.Tools;

/// <summary>
/// Returns a compact overview for a source file in the loaded solution.
/// </summary>
[McpServerToolType]
internal sealed class FileOverviewTool(INavigationService navigationService)
{
    /// <summary>
    /// Returns namespaces and type summaries for the requested source file.
    /// </summary>
    [McpServerTool(Name = "get_file_overview")]
    [Description("Returns a compact overview for a source file in the loaded solution.")]
    public ValueTask<GetFileOverviewResult> GetFileOverview(
        [Description("Absolute path to a previously loaded .sln, .slnx, or .csproj file.")] string solutionPath,
        [Description("Absolute path to the source file.")] string filePath,
        CancellationToken cancellationToken = default)
        => navigationService.GetFileOverviewAsync(
            new GetFileOverviewRequest(solutionPath, filePath),
            cancellationToken);
}
