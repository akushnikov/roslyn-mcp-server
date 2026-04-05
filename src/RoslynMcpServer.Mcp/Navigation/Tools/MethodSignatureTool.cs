using System.ComponentModel;
using ModelContextProtocol.Server;
using RoslynMcpServer.Abstractions.Navigation.Requests;
using RoslynMcpServer.Abstractions.Navigation.Results;
using RoslynMcpServer.Abstractions.Navigation.Services;

namespace RoslynMcpServer.Mcp.Navigation.Tools;

/// <summary>
/// Returns signature details for the callable member resolved at a specific source position.
/// </summary>
[McpServerToolType]
internal sealed class MethodSignatureTool(INavigationService navigationService)
{
    /// <summary>
    /// Resolves the callable member at the requested file position and returns its signature.
    /// </summary>
    [McpServerTool(Name = "get_method_signature")]
    [Description("Returns signature details for the callable member at the requested file position.")]
    public ValueTask<GetMethodSignatureResult> GetMethodSignature(
        [Description("Absolute path to a previously loaded .sln, .slnx, or .csproj file.")] string solutionPath,
        [Description("Absolute path to the source file that contains the callable member.")] string filePath,
        [Description("1-based line number of the target symbol location.")] int line,
        [Description("1-based column number of the target symbol location.")] int column,
        CancellationToken cancellationToken = default)
        => navigationService.GetMethodSignatureAsync(
            new GetMethodSignatureRequest(solutionPath, filePath, line, column),
            cancellationToken);
}
