using RoslynMcpServer.Abstractions.Navigation.Models;
using RoslynMcpServer.Abstractions.Navigation.Requests;
using RoslynMcpServer.Abstractions.Navigation.Results;
using RoslynMcpServer.Abstractions.Navigation.Services;
using RoslynMcpServer.Application.Navigation.Operations;
using RoslynMcpServer.Application.QueryPipeline;

namespace RoslynMcpServer.Application.Navigation;

/// <summary>
/// Projects find-references query outcomes to the public result contract.
/// </summary>
public sealed class FindReferencesQueryService(
    FindReferencesQueryOperation operation) : IFindReferencesQueryService
{
    /// <inheritdoc />
    public async ValueTask<FindReferencesResult> FindReferencesAsync(
        FindReferencesRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await operation.ExecuteAsync(request, cancellationToken);

        return QueryResultMapper.Map(
            result,
            static success => success,
            static error => new FindReferencesResult(
                Symbol: null,
                References: Array.Empty<SymbolReferenceDescriptor>(),
                FailureReason: error.FailureReason,
                Guidance: error.Guidance),
            static () => new FindReferencesResult(
                Symbol: null,
                References: Array.Empty<SymbolReferenceDescriptor>(),
                FailureReason: "The operation was canceled.",
                Guidance: "Retry the request when the operation can run to completion."));
    }
}
