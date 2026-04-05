using System.ComponentModel;
using ModelContextProtocol.Server;
using RoslynMcpServer.Abstractions.Navigation.Requests;
using RoslynMcpServer.Abstractions.Navigation.Results;
using RoslynMcpServer.Abstractions.Navigation.Services;

namespace RoslynMcpServer.Mcp.Navigation.Tools;

/// <summary>
/// Returns source references for the symbol resolved at a specific source position.
/// </summary>
[McpServerToolType]
internal sealed class FindReferencesTool(INavigationService navigationService)
{
    /// <summary>
    /// Finds references for the symbol resolved at the requested file position.
    /// </summary>
    [McpServerTool(Name = "find_references")]
    [Description("Finds source references for the symbol at the requested file position.")]
    public ValueTask<FindReferencesResult> FindReferences(
        [Description("Absolute path to a previously loaded .sln, .slnx, or .csproj file.")] string solutionPath,
        [Description("Absolute path to the source file that contains the symbol reference or declaration.")] string filePath,
        [Description("1-based line number of the target symbol location.")] int line,
        [Description("1-based column number of the target symbol location.")] int column,
        [Description("Maximum number of references to return.")] int maxResults = 100,
        CancellationToken cancellationToken = default)
        => navigationService.FindReferencesAsync(
            new FindReferencesRequest(solutionPath, filePath, line, column, maxResults),
            cancellationToken);
}
