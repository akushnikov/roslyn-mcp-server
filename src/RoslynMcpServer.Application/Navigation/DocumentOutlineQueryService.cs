using RoslynMcpServer.Abstractions.Navigation.Models;
using RoslynMcpServer.Abstractions.Navigation.Requests;
using RoslynMcpServer.Abstractions.Navigation.Results;
using RoslynMcpServer.Abstractions.Navigation.Services;
using RoslynMcpServer.Application.Navigation.Operations;
using RoslynMcpServer.Application.QueryPipeline;

namespace RoslynMcpServer.Application.Navigation;

/// <summary>Projects DocumentOutline pipeline outcomes into the public result contract.</summary>
public sealed class DocumentOutlineQueryService(DocumentOutlineQueryOperation operation) : IDocumentOutlineQueryService
{
    /// <inheritdoc />
    public async ValueTask<GetDocumentOutlineResult> GetDocumentOutlineAsync(
        GetDocumentOutlineRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await operation.ExecuteAsync(request, cancellationToken);

        return QueryResultMapper.Map(
            result,
            static success => success,
            static error => new GetDocumentOutlineResult(
                FilePath: error.FilePath,
                Nodes: Array.Empty<DocumentOutlineNode>(),
                FailureReason: error.FailureReason,
                Guidance: error.Guidance),
            static () => new GetDocumentOutlineResult(
                FilePath: null,
                Nodes: Array.Empty<DocumentOutlineNode>(),
                FailureReason: "The operation was canceled.",
                Guidance: "Retry the request when the operation can run to completion."));
    }
}
