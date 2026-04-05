using System.ComponentModel;
using ModelContextProtocol.Server;
using RoslynMcpServer.Abstractions.Navigation.Requests;
using RoslynMcpServer.Abstractions.Navigation.Results;
using RoslynMcpServer.Abstractions.Navigation.Services;

namespace RoslynMcpServer.Mcp.Navigation.Tools;

/// <summary>
/// Returns source definition locations for the symbol resolved at a specific source position.
/// </summary>
[McpServerToolType]
internal sealed class GoToDefinitionTool(INavigationService navigationService)
{
    /// <summary>
    /// Resolves source definition locations for the symbol at the requested file position.
    /// </summary>
    [McpServerTool(Name = "go_to_definition")]
    [Description("Resolves source definition locations for the symbol at the requested file position.")]
    public ValueTask<GoToDefinitionResult> GoToDefinition(
        [Description("Absolute path to a previously loaded .sln, .slnx, or .csproj file.")] string solutionPath,
        [Description("Absolute path to the source file that contains the symbol reference or declaration.")] string filePath,
        [Description("1-based line number of the target symbol location.")] int line,
        [Description("1-based column number of the target symbol location.")] int column,
        CancellationToken cancellationToken = default)
        => navigationService.GoToDefinitionAsync(
            new GoToDefinitionRequest(solutionPath, filePath, line, column),
            cancellationToken);
}
