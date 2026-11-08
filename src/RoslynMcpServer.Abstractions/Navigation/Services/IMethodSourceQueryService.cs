using RoslynMcpServer.Abstractions.Navigation.Requests;
using RoslynMcpServer.Abstractions.Navigation.Results;

namespace RoslynMcpServer.Abstractions.Navigation.Services;

/// <summary>Retrieves callable source through the validated query pipeline.</summary>
public interface IMethodSourceQueryService
{
    /// <summary>Retrieves the declaration source for the callable resolved at the requested position.</summary>
    /// <param name="request">A position in a source document of a previously loaded workspace.</param>
    /// <param name="cancellationToken">Cancels the query without producing an error outcome.</param>
    /// <returns>The declaration source and signature, or a structured failure retaining any resolved signature.</returns>
    ValueTask<GetMethodSourceResult> GetMethodSourceAsync(
        GetMethodSourceRequest request,
        CancellationToken cancellationToken = default);
}
