using RoslynMcpServer.Abstractions.Navigation.Requests;
using RoslynMcpServer.Abstractions.Navigation.Results;
using RoslynMcpServer.Abstractions.Navigation.Services;

namespace RoslynMcpServer.Infrastructure.Roslyn.Navigation;

/// <summary>Provides MethodSource query data; dormant until its migration owner switches the adapter.</summary>
internal sealed class RoslynMethodSourceQueryDataProvider : IMethodSourceQueryDataProvider
{
    /// <inheritdoc />
    public ValueTask<GetMethodSourceResult> GetMethodSourceAsync(
        GetMethodSourceRequest request,
        CancellationToken cancellationToken = default) =>
        throw new NotImplementedException("The MethodSource provider is implemented by its Phase 13 migration plan.");
}
