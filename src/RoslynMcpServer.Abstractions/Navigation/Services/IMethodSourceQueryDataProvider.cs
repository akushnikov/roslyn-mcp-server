using RoslynMcpServer.Abstractions.Navigation.Requests;
using RoslynMcpServer.Abstractions.Navigation.Results;

namespace RoslynMcpServer.Abstractions.Navigation.Services;

/// <summary>Resolves callable declarations without exposing Roslyn types to the application.</summary>
public interface IMethodSourceQueryDataProvider
{
    /// <summary>Reads source text, path and line span from the resolved declaration's document.</summary>
    /// <param name="request">The validated workspace and source position to resolve.</param>
    /// <param name="cancellationToken">Cancels workspace access and source resolution.</param>
    /// <returns>Callable source or a structured failure that preserves any resolved signature.</returns>
    ValueTask<GetMethodSourceResult> GetMethodSourceAsync(
        GetMethodSourceRequest request,
        CancellationToken cancellationToken = default);
}
