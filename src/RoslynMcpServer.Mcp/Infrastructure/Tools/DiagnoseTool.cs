using System.ComponentModel;
using ModelContextProtocol.Server;
using RoslynMcpServer.Abstractions.Infrastructure.Requests;
using RoslynMcpServer.Abstractions.Infrastructure.Results;
using RoslynMcpServer.Abstractions.Infrastructure.Services;

namespace RoslynMcpServer.Mcp.Infrastructure.Tools;

/// <summary>
/// Reports the server's environment health and optional workspace load diagnostics.
/// </summary>
[McpServerToolType]
internal sealed class DiagnoseTool(IServerDiagnosticsService diagnosticsService)
{
    /// <summary>
    /// Returns environment and optional workspace diagnostics for the current server instance.
    /// </summary>
    [McpServerTool(Name = "diagnose")]
    [Description("Checks server environment health and optionally probes a solution or project path.")]
    public ValueTask<DiagnoseResult> Diagnose(
        [Description("Optional absolute path to a .sln, .slnx, or .csproj file to probe.")] string? solutionPath,
        [Description("When true, include non-error diagnostics and extra environment detail.")] bool verbose = false,
        CancellationToken cancellationToken = default)
        => diagnosticsService.DiagnoseAsync(new DiagnoseRequest(solutionPath, verbose), cancellationToken);
}
