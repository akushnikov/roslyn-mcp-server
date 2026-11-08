using RoslynMcpServer.Abstractions.Navigation.Requests;
using RoslynMcpServer.Abstractions.Navigation.Results;

namespace RoslynMcpServer.Abstractions.Navigation.Services;

/// <summary>Supplies type overview data without exposing workspace implementation types.</summary>
public interface ITypeOverviewQueryDataProvider
{
    /// <summary>Resolves the named type and maps its direct relationships and explicit members.</summary>
    /// <param name="request">The solution and source position, validated by the query operation.</param>
    /// <param name="cancellationToken">The token whose requested cancellation must propagate.</param>
    /// <returns>The overview or the existing contextual failure details.</returns>
    ValueTask<GetTypeOverviewResult> GetTypeOverviewAsync(
        GetTypeOverviewRequest request,
        CancellationToken cancellationToken = default);
}
