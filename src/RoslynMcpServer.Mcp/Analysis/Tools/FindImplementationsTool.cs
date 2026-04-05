using System.ComponentModel;
using ModelContextProtocol.Server;
using RoslynMcpServer.Abstractions.Analysis.Requests;
using RoslynMcpServer.Abstractions.Analysis.Results;
using RoslynMcpServer.Abstractions.Analysis.Services;

namespace RoslynMcpServer.Mcp.Analysis.Tools;

/// <summary>
/// Returns implementations for the symbol resolved at a specific source position.
/// </summary>
[McpServerToolType]
internal sealed class FindImplementationsTool(IAnalysisService analysisService)
{
    /// <summary>
    /// Finds implementations of an interface, abstract class, or overridable member.
    /// </summary>
    [McpServerTool(Name = "find_implementations")]
    [Description("Finds implementations for the symbol at the requested file position.")]
    public ValueTask<FindImplementationsResult> FindImplementations(
        [Description("Absolute path to a previously loaded .sln, .slnx, or .csproj file.")] string solutionPath,
        [Description("Absolute path to the source file containing the symbol.")] string filePath,
        [Description("1-based line number of the target symbol location.")] int line,
        [Description("1-based column number of the target symbol location.")] int column,
        [Description("Maximum number of implementations to return.")] int maxResults = 100,
        CancellationToken cancellationToken = default)
        => analysisService.FindImplementationsAsync(
            new FindImplementationsRequest(solutionPath, filePath, line, column, maxResults),
            cancellationToken);
}
