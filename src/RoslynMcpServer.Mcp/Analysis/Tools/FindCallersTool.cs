using System.ComponentModel;
using ModelContextProtocol.Server;
using RoslynMcpServer.Abstractions.Analysis.Requests;
using RoslynMcpServer.Abstractions.Analysis.Results;
using RoslynMcpServer.Abstractions.Analysis.Services;

namespace RoslynMcpServer.Mcp.Analysis.Tools;

/// <summary>
/// Returns caller locations for the symbol resolved at a specific source position.
/// </summary>
[McpServerToolType]
internal sealed class FindCallersTool(IAnalysisService analysisService)
{
    /// <summary>
    /// Finds callers for the symbol at the requested file position.
    /// </summary>
    [McpServerTool(Name = "find_callers")]
    [Description("Finds all callers of a method or symbol across the loaded solution.")]
    public ValueTask<FindCallersResult> FindCallers(
        [Description("Absolute path to a previously loaded .sln, .slnx, or .csproj file.")] string solutionPath,
        [Description("Absolute path to the source file containing the symbol.")] string filePath,
        [Description("1-based line number of the target symbol location.")] int line,
        [Description("1-based column number of the target symbol location.")] int column,
        [Description("Maximum number of callers to return.")] int maxResults = 100,
        CancellationToken cancellationToken = default)
        => analysisService.FindCallersAsync(
            new FindCallersRequest(solutionPath, filePath, line, column, maxResults),
            cancellationToken);
}
