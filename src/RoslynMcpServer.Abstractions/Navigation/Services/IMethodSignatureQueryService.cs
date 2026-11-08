using RoslynMcpServer.Abstractions.Navigation.Requests;
using RoslynMcpServer.Abstractions.Navigation.Results;

namespace RoslynMcpServer.Abstractions.Navigation.Services;

/// <summary>Defines the MethodSignature query application boundary.</summary>
public interface IMethodSignatureQueryService
{
    /// <summary>Retrieves MethodSignature data for a document in a loaded workspace.</summary>
    ValueTask<GetMethodSignatureResult> GetMethodSignatureAsync(
        GetMethodSignatureRequest request,
        CancellationToken cancellationToken = default);
}
