using RoslynMcpServer.Abstractions.Navigation.Requests;
using RoslynMcpServer.Abstractions.Navigation.Results;
using RoslynMcpServer.Abstractions.Navigation.Services;

namespace RoslynMcpServer.Infrastructure.Roslyn.Navigation;

/// <summary>Provides TypeOverview query data; dormant until its migration owner switches the adapter.</summary>
internal sealed class RoslynTypeOverviewQueryDataProvider : ITypeOverviewQueryDataProvider
{
    /// <inheritdoc />
    public ValueTask<GetTypeOverviewResult> GetTypeOverviewAsync(
        GetTypeOverviewRequest request,
        CancellationToken cancellationToken = default) =>
        throw new NotImplementedException("The TypeOverview provider is implemented by its Phase 13 migration plan.");
}
