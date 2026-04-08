using System.ComponentModel;
using ModelContextProtocol.Server;
using RoslynMcpServer.Abstractions.Navigation.Requests;
using RoslynMcpServer.Abstractions.Navigation.Results;
using RoslynMcpServer.Abstractions.Navigation.Services;

namespace RoslynMcpServer.Mcp.Navigation.Tools;

/// <summary>
/// Returns semantic symbol information for a specific source position.
/// </summary>
[McpServerToolType]
internal sealed class SymbolInfoTool(ISymbolInfoQueryService symbolInfoQueryService)
{
    /// <summary>
    /// Resolves the symbol at the requested file position and returns its semantic metadata.
    /// </summary>
    [McpServerTool(Name = "get_symbol_info")]
    [Description("Returns semantic information for the symbol at the requested file position.")]
    public ValueTask<GetSymbolInfoResult> GetSymbolInfo(
        [Description("Absolute path to a previously loaded .sln, .slnx, or .csproj file.")] string solutionPath,
        [Description("Absolute path to the source file that contains the symbol reference or declaration.")] string filePath,
        [Description("1-based line number of the target symbol location.")] int line,
        [Description("1-based column number of the target symbol location.")] int column,
        CancellationToken cancellationToken = default)
        => symbolInfoQueryService.GetSymbolInfoAsync(
            new GetSymbolInfoRequest(solutionPath, filePath, line, column),
            cancellationToken);
}
