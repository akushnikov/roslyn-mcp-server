using RoslynMcpServer.Abstractions.Navigation.Requests;
using RoslynMcpServer.Abstractions.Navigation.Results;

namespace RoslynMcpServer.Abstractions.Navigation.Services;

/// <summary>Retrieves callable signatures through the validated navigation query pipeline.</summary>
public interface IMethodSignatureQueryService
{
    /// <summary>Returns the signature of a method, property accessor, or event accessor at the requested position.</summary>
    /// <param name="request">The loaded solution, source document, and 1-based position to inspect.</param>
    /// <param name="cancellationToken">Cancels the query and produces the standard cancellation result.</param>
    /// <returns>The callable signature or a structured failure with retry guidance.</returns>
    ValueTask<GetMethodSignatureResult> GetMethodSignatureAsync(
        GetMethodSignatureRequest request,
        CancellationToken cancellationToken = default);
}
