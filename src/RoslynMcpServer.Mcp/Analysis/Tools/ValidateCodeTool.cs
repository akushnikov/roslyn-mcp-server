using System.ComponentModel;
using ModelContextProtocol.Server;
using RoslynMcpServer.Abstractions.Analysis.Requests;
using RoslynMcpServer.Abstractions.Analysis.Results;
using RoslynMcpServer.Abstractions.Analysis.Services;

namespace RoslynMcpServer.Mcp.Analysis.Tools;

/// <summary>
/// Returns a validation summary for a loaded solution or source file.
/// </summary>
[McpServerToolType]
internal sealed class ValidateCodeTool(IAnalysisService analysisService)
{
    /// <summary>
    /// Validates the loaded solution or a specific file and reports whether compiler errors are present.
    /// </summary>
    [McpServerTool(Name = "validate_code")]
    [Description("Returns a validation summary for the loaded solution or a specific source file.")]
    public ValueTask<ValidateCodeResult> ValidateCode(
        [Description("Absolute path to a previously loaded .sln, .slnx, or .csproj file.")] string solutionPath,
        [Description("Optional absolute path to a source file to restrict validation to.")] string? filePath = null,
        CancellationToken cancellationToken = default)
        => analysisService.ValidateCodeAsync(
            new ValidateCodeRequest(solutionPath, filePath),
            cancellationToken);
}
