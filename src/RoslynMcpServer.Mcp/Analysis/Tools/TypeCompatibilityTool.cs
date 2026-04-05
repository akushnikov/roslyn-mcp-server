using System.ComponentModel;
using ModelContextProtocol.Server;
using RoslynMcpServer.Abstractions.Analysis.Requests;
using RoslynMcpServer.Abstractions.Analysis.Results;
using RoslynMcpServer.Abstractions.Analysis.Services;

namespace RoslynMcpServer.Mcp.Analysis.Tools;

/// <summary>
/// Returns a compatibility check between two resolved types.
/// </summary>
[McpServerToolType]
internal sealed class TypeCompatibilityTool(IAnalysisService analysisService)
{
    /// <summary>
    /// Resolves source and target types at the requested file positions and returns Roslyn conversion compatibility information.
    /// </summary>
    [McpServerTool(Name = "check_type_compatibility")]
    [Description("Returns a compatibility check between two resolved types.")]
    public ValueTask<CheckTypeCompatibilityResult> CheckTypeCompatibility(
        [Description("Absolute path to a previously loaded .sln, .slnx, or .csproj file.")] string solutionPath,
        [Description("Absolute path to the source file that contains the source type.")] string sourceFilePath,
        [Description("1-based line number of the source type location.")] int sourceLine,
        [Description("1-based column number of the source type location.")] int sourceColumn,
        [Description("Absolute path to the source file that contains the target type.")] string targetFilePath,
        [Description("1-based line number of the target type location.")] int targetLine,
        [Description("1-based column number of the target type location.")] int targetColumn,
        CancellationToken cancellationToken = default)
        => analysisService.CheckTypeCompatibilityAsync(
            new CheckTypeCompatibilityRequest(
                solutionPath,
                sourceFilePath,
                sourceLine,
                sourceColumn,
                targetFilePath,
                targetLine,
                targetColumn),
            cancellationToken);
}
