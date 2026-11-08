using RoslynMcpServer.Abstractions.Navigation.Requests;
using RoslynMcpServer.Abstractions.Navigation.Results;

namespace RoslynMcpServer.Abstractions.Navigation.Services;

/// <summary>Defines the TypeOverview query data source.</summary>
public interface ITypeOverviewQueryDataProvider
{
    /// <summary>Retrieves TypeOverview data for a document in a loaded workspace.</summary>
    ValueTask<GetTypeOverviewResult> GetTypeOverviewAsync(
        GetTypeOverviewRequest request,
        CancellationToken cancellationToken = default);
}
