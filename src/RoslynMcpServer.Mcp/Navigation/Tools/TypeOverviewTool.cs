using System.ComponentModel;
using ModelContextProtocol.Server;
using RoslynMcpServer.Abstractions.Navigation.Requests;
using RoslynMcpServer.Abstractions.Navigation.Results;
using RoslynMcpServer.Abstractions.Navigation.Services;

namespace RoslynMcpServer.Mcp.Navigation.Tools;

/// <summary>
/// Returns a compact overview for the type resolved at a specific source position.
/// </summary>
[McpServerToolType]
internal sealed class TypeOverviewTool(INavigationService navigationService)
{
    /// <summary>
    /// Resolves the type at the requested file position and returns base types, interfaces, and member summaries.
    /// </summary>
    [McpServerTool(Name = "get_type_overview")]
    [Description("Returns a compact overview for the type resolved at the requested file position.")]
    public ValueTask<GetTypeOverviewResult> GetTypeOverview(
        [Description("Absolute path to a previously loaded .sln, .slnx, or .csproj file.")] string solutionPath,
        [Description("Absolute path to the source file that contains the type or one of its members.")] string filePath,
        [Description("1-based line number of the target type or member location.")] int line,
        [Description("1-based column number of the target type or member location.")] int column,
        CancellationToken cancellationToken = default)
        => navigationService.GetTypeOverviewAsync(
            new GetTypeOverviewRequest(solutionPath, filePath, line, column),
            cancellationToken);
}
