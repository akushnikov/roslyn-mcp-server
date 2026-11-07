using RoslynMcpServer.Abstractions.Navigation.Requests;
using RoslynMcpServer.Abstractions.Navigation.Results;

namespace RoslynMcpServer.Abstractions.Navigation.Services;

/// <summary>
/// Defines the backend data provider contract for find-references query operations.
/// </summary>
public interface IFindReferencesQueryDataProvider
{
    /// <summary>
    /// Resolves source references from the underlying workspace backend.
    /// </summary>
    ValueTask<FindReferencesResult> FindReferencesAsync(
        FindReferencesRequest request,
        CancellationToken cancellationToken = default);
}
