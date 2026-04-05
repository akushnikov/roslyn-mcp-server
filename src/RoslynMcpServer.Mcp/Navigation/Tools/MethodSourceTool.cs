using System.ComponentModel;
using ModelContextProtocol.Server;
using RoslynMcpServer.Abstractions.Navigation.Requests;
using RoslynMcpServer.Abstractions.Navigation.Results;
using RoslynMcpServer.Abstractions.Navigation.Services;

namespace RoslynMcpServer.Mcp.Navigation.Tools;

/// <summary>
/// Returns source text for the callable member resolved at a specific source position.
/// </summary>
[McpServerToolType]
internal sealed class MethodSourceTool(INavigationService navigationService)
{
    /// <summary>
    /// Resolves the callable member at the requested file position and returns its source span and source text.
    /// </summary>
    [McpServerTool(Name = "get_method_source")]
    [Description("Returns source text for the callable member at the requested file position.")]
    public ValueTask<GetMethodSourceResult> GetMethodSource(
        [Description("Absolute path to a previously loaded .sln, .slnx, or .csproj file.")] string solutionPath,
        [Description("Absolute path to the source file that contains the callable member.")] string filePath,
        [Description("1-based line number of the target symbol location.")] int line,
        [Description("1-based column number of the target symbol location.")] int column,
        CancellationToken cancellationToken = default)
        => navigationService.GetMethodSourceAsync(
            new GetMethodSourceRequest(solutionPath, filePath, line, column),
            cancellationToken);
}
