using RoslynMcpServer.Abstractions.Navigation.Requests;
using RoslynMcpServer.Abstractions.Navigation.Results;

namespace RoslynMcpServer.Abstractions.Navigation.Services;

/// <summary>Retrieves attributes for a symbol through the application query pipeline.</summary>
public interface ISymbolAttributesQueryService
{
    /// <summary>Resolves a source position and returns the symbol's attributes ordered by name.</summary>
    /// <param name="request">The loaded solution and 1-based source position to inspect.</param>
    /// <param name="cancellationToken">Cancels symbol resolution and attribute retrieval.</param>
    /// <returns>The attributes or a structured failure retaining any resolved symbol.</returns>
    ValueTask<GetSymbolAttributesResult> GetSymbolAttributesAsync(
        GetSymbolAttributesRequest request,
        CancellationToken cancellationToken = default);
}
