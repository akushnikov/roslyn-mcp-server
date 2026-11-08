using RoslynMcpServer.Abstractions.Navigation.Requests;
using RoslynMcpServer.Abstractions.Navigation.Results;

namespace RoslynMcpServer.Abstractions.Navigation.Services;

/// <summary>Provides a structural document outline through the application query pipeline.</summary>
public interface IDocumentOutlineQueryService
{
    /// <summary>Retrieves the ordered declaration hierarchy for a source document.</summary>
    /// <param name="request">The loaded solution and source document to inspect.</param>
    /// <param name="cancellationToken">Cancels the query without reporting a server fault.</param>
    /// <returns>The outline or a structured failure retaining the document path available at failure.</returns>
    ValueTask<GetDocumentOutlineResult> GetDocumentOutlineAsync(
        GetDocumentOutlineRequest request,
        CancellationToken cancellationToken = default);
}
