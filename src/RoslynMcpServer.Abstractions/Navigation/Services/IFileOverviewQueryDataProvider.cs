using RoslynMcpServer.Abstractions.Navigation.Requests;
using RoslynMcpServer.Abstractions.Navigation.Results;

namespace RoslynMcpServer.Abstractions.Navigation.Services;

/// <summary>Defines the FileOverview query data source.</summary>
public interface IFileOverviewQueryDataProvider
{
    /// <summary>Retrieves FileOverview data for a document in a loaded workspace.</summary>
    ValueTask<GetFileOverviewResult> GetFileOverviewAsync(
        GetFileOverviewRequest request,
        CancellationToken cancellationToken = default);
}
