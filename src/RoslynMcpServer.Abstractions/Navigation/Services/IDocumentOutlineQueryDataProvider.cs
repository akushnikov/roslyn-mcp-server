using RoslynMcpServer.Abstractions.Navigation.Requests;
using RoslynMcpServer.Abstractions.Navigation.Results;

namespace RoslynMcpServer.Abstractions.Navigation.Services;

/// <summary>Supplies document declarations without exposing compiler types across the application boundary.</summary>
public interface IDocumentOutlineQueryDataProvider
{
    /// <summary>Reads the declaration hierarchy of a document in a loaded solution.</summary>
    /// <param name="request">The validated solution and document paths.</param>
    /// <param name="cancellationToken">Cancels workspace and syntax retrieval.</param>
    /// <returns>Ordered declarations or a structured failure with stage-specific document context.</returns>
    ValueTask<GetDocumentOutlineResult> GetDocumentOutlineAsync(
        GetDocumentOutlineRequest request,
        CancellationToken cancellationToken = default);
}
