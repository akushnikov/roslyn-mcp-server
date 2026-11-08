using RoslynMcpServer.Abstractions.Navigation.Requests;
using RoslynMcpServer.Abstractions.Navigation.Results;

namespace RoslynMcpServer.Abstractions.Navigation.Services;

/// <summary>Provides semantic symbol attributes from a loaded workspace.</summary>
public interface ISymbolAttributesQueryDataProvider
{
    /// <summary>Returns the resolved symbol and its formatted attribute arguments.</summary>
    /// <param name="request">The validated solution, document and 1-based position.</param>
    /// <param name="cancellationToken">Cancels semantic resolution and attribute retrieval.</param>
    /// <returns>Attributes ordered by name or the existing structured lookup failure.</returns>
    ValueTask<GetSymbolAttributesResult> GetSymbolAttributesAsync(
        GetSymbolAttributesRequest request,
        CancellationToken cancellationToken = default);
}
