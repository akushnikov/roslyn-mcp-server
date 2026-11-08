using RoslynMcpServer.Abstractions.Navigation.Requests;
using RoslynMcpServer.Abstractions.Navigation.Results;

namespace RoslynMcpServer.Abstractions.Navigation.Services;

/// <summary>Retrieves a named type's base type, direct interfaces, and explicit member summaries.</summary>
public interface ITypeOverviewQueryService
{
    /// <summary>Resolves a type or its containing type at a source position in a loaded workspace.</summary>
    /// <param name="request">The loaded solution and 1-based source position to inspect.</param>
    /// <param name="cancellationToken">The token used to cancel the lookup.</param>
    /// <returns>The overview, a contextual failure, or the stable cancellation result.</returns>
    ValueTask<GetTypeOverviewResult> GetTypeOverviewAsync(
        GetTypeOverviewRequest request,
        CancellationToken cancellationToken = default);
}
