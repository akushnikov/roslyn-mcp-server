using System.ComponentModel;
using ModelContextProtocol.Server;
using RoslynMcpServer.Abstractions.Navigation.Requests;
using RoslynMcpServer.Abstractions.Navigation.Results;
using RoslynMcpServer.Abstractions.Navigation.Services;

namespace RoslynMcpServer.Mcp.Navigation.Tools;

/// <summary>
/// Returns a declaration outline for a source file in a loaded solution.
/// </summary>
[McpServerToolType]
internal sealed class DocumentOutlineTool(INavigationService navigationService)
{
    /// <summary>
    /// Returns top-level and nested declarations for a source document.
    /// </summary>
    [McpServerTool(Name = "get_document_outline")]
    [Description("Returns a structural outline for a source document in a previously loaded solution.")]
    public ValueTask<GetDocumentOutlineResult> GetDocumentOutline(
        [Description("Absolute path to a previously loaded .sln, .slnx, or .csproj file.")] string solutionPath,
        [Description("Absolute path to a source file that belongs to the loaded solution.")] string filePath,
        CancellationToken cancellationToken = default)
        => navigationService.GetDocumentOutlineAsync(
            new GetDocumentOutlineRequest(solutionPath, filePath),
            cancellationToken);
}
