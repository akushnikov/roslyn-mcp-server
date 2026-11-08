using RoslynMcpServer.Abstractions.Navigation.Models;
using RoslynMcpServer.Abstractions.Navigation.Requests;
using RoslynMcpServer.Abstractions.Navigation.Results;
using RoslynMcpServer.Abstractions.Navigation.Services;
using RoslynMcpServer.Application.Navigation.Operations;
using RoslynMcpServer.Application.QueryPipeline;

namespace RoslynMcpServer.Application.Navigation;

/// <summary>Projects MethodSignature pipeline outcomes into the public result contract.</summary>
public sealed class MethodSignatureQueryService(MethodSignatureQueryOperation operation) : IMethodSignatureQueryService
{
    /// <inheritdoc />
    public async ValueTask<GetMethodSignatureResult> GetMethodSignatureAsync(
        GetMethodSignatureRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await operation.ExecuteAsync(request, cancellationToken);

        return QueryResultMapper.Map(
            result,
            static success => success,
            static error => new GetMethodSignatureResult(
                Signature: null,
                FailureReason: error.FailureReason,
                Guidance: error.Guidance),
            static () => new GetMethodSignatureResult(
                Signature: null,
                FailureReason: "The operation was canceled.",
                Guidance: "Retry the request when the operation can run to completion."));
    }
}
