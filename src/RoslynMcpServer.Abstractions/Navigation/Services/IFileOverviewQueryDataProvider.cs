using RoslynMcpServer.Abstractions.Navigation.Requests;
using RoslynMcpServer.Abstractions.Navigation.Results;

namespace RoslynMcpServer.Abstractions.Navigation.Services;

/// <summary>Provides source-file overview data independently of transport and pipeline policy.</summary>
public interface IFileOverviewQueryDataProvider
{
    /// <summary>Retrieves namespaces and types from a document in the requested loaded solution.</summary>
    /// <param name="request">The loaded solution and document paths.</param>
    /// <param name="cancellationToken">Cancels workspace and syntax access.</param>
    /// <returns>The overview or a structured failure with the path available at the failure stage.</returns>
    ValueTask<GetFileOverviewResult> GetFileOverviewAsync(
        GetFileOverviewRequest request,
        CancellationToken cancellationToken = default);
}
