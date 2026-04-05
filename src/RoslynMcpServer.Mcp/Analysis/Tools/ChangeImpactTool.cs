using System.ComponentModel;
using ModelContextProtocol.Server;
using RoslynMcpServer.Abstractions.Analysis.Requests;
using RoslynMcpServer.Abstractions.Analysis.Results;
using RoslynMcpServer.Abstractions.Analysis.Services;

namespace RoslynMcpServer.Mcp.Analysis.Tools;

/// <summary>
/// Returns a best-effort semantic impact summary for the symbol resolved at a specific source position.
/// </summary>
[McpServerToolType]
internal sealed class ChangeImpactTool(IAnalysisService analysisService)
{
    /// <summary>
    /// Resolves the symbol at the requested file position and returns references, callers, implementations, and impacted files.
    /// </summary>
    [McpServerTool(Name = "analyze_change_impact")]
    [Description("Returns a best-effort semantic impact summary for the symbol at the requested file position.")]
    public ValueTask<AnalyzeChangeImpactResult> AnalyzeChangeImpact(
        [Description("Absolute path to a previously loaded .sln, .slnx, or .csproj file.")] string solutionPath,
        [Description("Absolute path to the source file that contains the symbol reference or declaration.")] string filePath,
        [Description("1-based line number of the target symbol location.")] int line,
        [Description("1-based column number of the target symbol location.")] int column,
        [Description("Maximum number of references, callers, and implementations to include.")] int maxResults = 100,
        CancellationToken cancellationToken = default)
        => analysisService.AnalyzeChangeImpactAsync(
            new AnalyzeChangeImpactRequest(solutionPath, filePath, line, column, maxResults),
            cancellationToken);
}
