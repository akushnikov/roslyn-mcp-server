using RoslynMcpServer.Abstractions.Navigation.Requests;
using RoslynMcpServer.Abstractions.Navigation.Results;
using RoslynMcpServer.Abstractions.Navigation.Services;

namespace RoslynMcpServer.Infrastructure.Roslyn.Navigation;

/// <summary>Provides FileOverview query data; dormant until its migration owner switches the adapter.</summary>
internal sealed class RoslynFileOverviewQueryDataProvider : IFileOverviewQueryDataProvider
{
    /// <inheritdoc />
    public ValueTask<GetFileOverviewResult> GetFileOverviewAsync(
        GetFileOverviewRequest request,
        CancellationToken cancellationToken = default) =>
        throw new NotImplementedException("The FileOverview provider is implemented by its Phase 13 migration plan.");
}
