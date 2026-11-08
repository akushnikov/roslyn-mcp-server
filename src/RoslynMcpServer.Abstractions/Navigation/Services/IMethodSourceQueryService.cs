using RoslynMcpServer.Abstractions.Navigation.Requests;
using RoslynMcpServer.Abstractions.Navigation.Results;

namespace RoslynMcpServer.Abstractions.Navigation.Services;

/// <summary>Defines the MethodSource query application boundary.</summary>
public interface IMethodSourceQueryService
{
    /// <summary>Retrieves MethodSource data for a document in a loaded workspace.</summary>
    ValueTask<GetMethodSourceResult> GetMethodSourceAsync(
        GetMethodSourceRequest request,
        CancellationToken cancellationToken = default);
}
