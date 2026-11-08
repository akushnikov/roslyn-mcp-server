using RoslynMcpServer.Abstractions.Navigation.Requests;
using RoslynMcpServer.Abstractions.Navigation.Results;

namespace RoslynMcpServer.Abstractions.Navigation.Services;

/// <summary>Defines the MethodSource query data source.</summary>
public interface IMethodSourceQueryDataProvider
{
    /// <summary>Retrieves MethodSource data for a document in a loaded workspace.</summary>
    ValueTask<GetMethodSourceResult> GetMethodSourceAsync(
        GetMethodSourceRequest request,
        CancellationToken cancellationToken = default);
}
