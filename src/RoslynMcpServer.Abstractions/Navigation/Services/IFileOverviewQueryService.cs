using RoslynMcpServer.Abstractions.Navigation.Requests;
using RoslynMcpServer.Abstractions.Navigation.Results;

namespace RoslynMcpServer.Abstractions.Navigation.Services;

/// <summary>Defines the FileOverview query application boundary.</summary>
public interface IFileOverviewQueryService
{
    /// <summary>Retrieves FileOverview data for a document in a loaded workspace.</summary>
    ValueTask<GetFileOverviewResult> GetFileOverviewAsync(
        GetFileOverviewRequest request,
        CancellationToken cancellationToken = default);
}
