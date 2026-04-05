using System.ComponentModel;
using ModelContextProtocol.Server;
using RoslynMcpServer.Abstractions.Analysis.Requests;
using RoslynMcpServer.Abstractions.Analysis.Results;
using RoslynMcpServer.Abstractions.Analysis.Services;

namespace RoslynMcpServer.Mcp.Analysis.Tools;

/// <summary>
/// Returns type hierarchy information for the symbol resolved at a specific source position.
/// </summary>
[McpServerToolType]
internal sealed class TypeHierarchyTool(IAnalysisService analysisService)
{
    /// <summary>
    /// Returns base types, derived types, and interfaces for the requested symbol.
    /// </summary>
    [McpServerTool(Name = "get_type_hierarchy")]
    [Description("Returns the type hierarchy for the symbol at the requested file position.")]
    public ValueTask<GetTypeHierarchyResult> GetTypeHierarchy(
        [Description("Absolute path to a previously loaded .sln, .slnx, or .csproj file.")] string solutionPath,
        [Description("Absolute path to the source file containing the symbol.")] string filePath,
        [Description("1-based line number of the target symbol location.")] int line,
        [Description("1-based column number of the target symbol location.")] int column,
        [Description("Hierarchy direction: Ancestors, Descendants, or Both.")] string? direction = null,
        CancellationToken cancellationToken = default)
        => analysisService.GetTypeHierarchyAsync(
            new GetTypeHierarchyRequest(solutionPath, filePath, line, column, direction),
            cancellationToken);
}
