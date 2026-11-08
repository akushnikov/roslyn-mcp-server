using RoslynMcpServer.Abstractions.Navigation.Requests;
using RoslynMcpServer.Abstractions.Navigation.Results;

namespace RoslynMcpServer.Abstractions.Navigation.Services;

/// <summary>Provides callable signatures without exposing Roslyn types to the application layer.</summary>
public interface IMethodSignatureQueryDataProvider
{
    /// <summary>Resolves a callable signature after the operation has validated the request.</summary>
    /// <param name="request">The loaded solution, source document, and 1-based position to inspect.</param>
    /// <param name="cancellationToken">Cancels symbol resolution; requested cancellation propagates to the operation.</param>
    /// <returns>The signature, or the existing resolution or unsupported-symbol failure.</returns>
    ValueTask<GetMethodSignatureResult> GetMethodSignatureAsync(
        GetMethodSignatureRequest request,
        CancellationToken cancellationToken = default);
}
