using RoslynMcpServer.Abstractions.Navigation.Requests;
using RoslynMcpServer.Abstractions.Navigation.Results;
using RoslynMcpServer.Abstractions.Navigation.Services;

namespace RoslynMcpServer.Infrastructure.Roslyn.Navigation;

/// <summary>Provides DocumentOutline query data; dormant until its migration owner switches the adapter.</summary>
internal sealed class RoslynDocumentOutlineQueryDataProvider : IDocumentOutlineQueryDataProvider
{
    /// <inheritdoc />
    public ValueTask<GetDocumentOutlineResult> GetDocumentOutlineAsync(
        GetDocumentOutlineRequest request,
        CancellationToken cancellationToken = default) =>
        throw new NotImplementedException("The DocumentOutline provider is implemented by its Phase 13 migration plan.");
}
