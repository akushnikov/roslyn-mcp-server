using RoslynMcpServer.Abstractions.Navigation.Requests;
using RoslynMcpServer.Abstractions.Navigation.Results;

namespace RoslynMcpServer.Abstractions.Navigation.Services;

/// <summary>Defines the SymbolAttributes query application boundary.</summary>
public interface ISymbolAttributesQueryService
{
    /// <summary>Retrieves SymbolAttributes data for a document in a loaded workspace.</summary>
    ValueTask<GetSymbolAttributesResult> GetSymbolAttributesAsync(
        GetSymbolAttributesRequest request,
        CancellationToken cancellationToken = default);
}
