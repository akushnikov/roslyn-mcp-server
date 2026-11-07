using RoslynMcpServer.Abstractions.Navigation.Requests;
using RoslynMcpServer.Abstractions.Navigation.Results;

namespace RoslynMcpServer.Abstractions.Navigation.Services;

/// <summary>
/// Defines the backend data provider contract for go-to-definition query operations.
/// </summary>
public interface IGoToDefinitionQueryDataProvider
{
    /// <summary>
    /// Resolves source definition locations from the underlying workspace backend.
    /// </summary>
    ValueTask<GoToDefinitionResult> GoToDefinitionAsync(
        GoToDefinitionRequest request,
        CancellationToken cancellationToken = default);
}
