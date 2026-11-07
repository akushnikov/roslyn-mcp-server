using RoslynMcpServer.Abstractions.Navigation.Requests;
using RoslynMcpServer.Abstractions.Navigation.Results;

namespace RoslynMcpServer.Abstractions.Navigation.Services;

/// <summary>
/// Defines the application boundary for the find-references query used by MCP adapters.
/// </summary>
public interface IFindReferencesQueryService
{
    /// <summary>
    /// Returns source references resolved for the symbol at a source position.
    /// </summary>
    ValueTask<FindReferencesResult> FindReferencesAsync(
        FindReferencesRequest request,
        CancellationToken cancellationToken = default);
}
