using System.ComponentModel;
using ModelContextProtocol.Server;
using RoslynMcpServer.Abstractions.Analysis.Requests;
using RoslynMcpServer.Abstractions.Analysis.Results;
using RoslynMcpServer.Abstractions.Analysis.Services;

namespace RoslynMcpServer.Mcp.Analysis.Tools;

/// <summary>
/// Returns outgoing calls for the callable member resolved at a specific source position.
/// </summary>
[McpServerToolType]
internal sealed class OutgoingCallsTool(IAnalysisService analysisService)
{
    /// <summary>
    /// Resolves the callable member at the requested file position and returns its outgoing calls.
    /// </summary>
    [McpServerTool(Name = "get_outgoing_calls")]
    [Description("Returns outgoing calls for the callable member at the requested file position.")]
    public ValueTask<GetOutgoingCallsResult> GetOutgoingCalls(
        [Description("Absolute path to a previously loaded .sln, .slnx, or .csproj file.")] string solutionPath,
        [Description("Absolute path to the source file that contains the callable member.")] string filePath,
        [Description("1-based line number of the target symbol location.")] int line,
        [Description("1-based column number of the target symbol location.")] int column,
        [Description("Maximum number of outgoing calls to return.")] int maxResults = 100,
        CancellationToken cancellationToken = default)
        => analysisService.GetOutgoingCallsAsync(
            new GetOutgoingCallsRequest(solutionPath, filePath, line, column, maxResults),
            cancellationToken);
}
