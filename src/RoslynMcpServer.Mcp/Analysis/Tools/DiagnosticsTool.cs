using System.ComponentModel;
using ModelContextProtocol.Server;
using RoslynMcpServer.Abstractions.Analysis.Requests;
using RoslynMcpServer.Abstractions.Analysis.Results;
using RoslynMcpServer.Abstractions.Analysis.Services;

namespace RoslynMcpServer.Mcp.Analysis.Tools;

/// <summary>
/// Returns compiler diagnostics for a loaded solution or a specific source file.
/// </summary>
[McpServerToolType]
internal sealed class DiagnosticsTool(IAnalysisService analysisService)
{
    /// <summary>
    /// Returns compiler diagnostics for a loaded solution or source file.
    /// </summary>
    [McpServerTool(Name = "get_diagnostics")]
    [Description("Gets compiler diagnostics for the loaded solution or a specific source file.")]
    public ValueTask<GetDiagnosticsResult> GetDiagnostics(
        [Description("Absolute path to a previously loaded .sln, .slnx, or .csproj file.")] string solutionPath,
        [Description("Optional absolute path to a source file to restrict diagnostics to.")] string? filePath = null,
        [Description("Optional severity filter: Error, Warning, Info, Hidden, or All.")] string? severityFilter = null,
        CancellationToken cancellationToken = default)
        => analysisService.GetDiagnosticsAsync(
            new GetDiagnosticsRequest(solutionPath, filePath, severityFilter),
            cancellationToken);
}
