using RoslynMcpServer.Abstractions.Navigation.Requests;
using RoslynMcpServer.Abstractions.Navigation.Results;
using RoslynMcpServer.Abstractions.Navigation.Services;

namespace RoslynMcpServer.Infrastructure.Roslyn.Navigation;

/// <summary>Provides SymbolAttributes query data; dormant until its migration owner switches the adapter.</summary>
internal sealed class RoslynSymbolAttributesQueryDataProvider : ISymbolAttributesQueryDataProvider
{
    /// <inheritdoc />
    public ValueTask<GetSymbolAttributesResult> GetSymbolAttributesAsync(
        GetSymbolAttributesRequest request,
        CancellationToken cancellationToken = default) =>
        throw new NotImplementedException("The SymbolAttributes provider is implemented by its Phase 13 migration plan.");
}
