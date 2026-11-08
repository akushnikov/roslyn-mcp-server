using RoslynMcpServer.Abstractions.Navigation.Requests;
using RoslynMcpServer.Abstractions.Navigation.Results;

namespace RoslynMcpServer.Abstractions.Navigation.Services;

/// <summary>Defines the DocumentOutline query application boundary.</summary>
public interface IDocumentOutlineQueryService
{
    /// <summary>Retrieves DocumentOutline data for a document in a loaded workspace.</summary>
    ValueTask<GetDocumentOutlineResult> GetDocumentOutlineAsync(
        GetDocumentOutlineRequest request,
        CancellationToken cancellationToken = default);
}
