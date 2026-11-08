using RoslynMcpServer.Abstractions.Navigation.Models;
using RoslynMcpServer.Abstractions.Navigation.Requests;
using RoslynMcpServer.Abstractions.Navigation.Results;
using RoslynMcpServer.Abstractions.Navigation.Services;
using RoslynMcpServer.Application.Navigation.Operations;
using RoslynMcpServer.Application.QueryPipeline;

namespace RoslynMcpServer.Application.Navigation;

/// <summary>Projects MethodSource pipeline outcomes into the public result contract.</summary>
public sealed class MethodSourceQueryService(MethodSourceQueryOperation operation) : IMethodSourceQueryService
{
    /// <inheritdoc />
    public async ValueTask<GetMethodSourceResult> GetMethodSourceAsync(
        GetMethodSourceRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await operation.ExecuteAsync(request, cancellationToken);

        return QueryResultMapper.Map(
            result,
            static success => success,
            static error => new GetMethodSourceResult(
                Signature: error.Signature,
                Method: null,
                FailureReason: error.FailureReason,
                Guidance: error.Guidance),
            static () => new GetMethodSourceResult(
                Signature: null,
                Method: null,
                FailureReason: "The operation was canceled.",
                Guidance: "Retry the request when the operation can run to completion."));
    }
}
