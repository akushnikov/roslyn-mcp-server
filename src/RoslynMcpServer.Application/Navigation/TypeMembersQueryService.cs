using RoslynMcpServer.Abstractions.Navigation.Models;
using RoslynMcpServer.Abstractions.Navigation.Requests;
using RoslynMcpServer.Abstractions.Navigation.Results;
using RoslynMcpServer.Abstractions.Navigation.Services;
using RoslynMcpServer.Application.Navigation.Operations;
using RoslynMcpServer.Application.QueryPipeline;

namespace RoslynMcpServer.Application.Navigation;

/// <summary>
/// Projects type-members pipeline outcomes into the public query result.
/// </summary>
public sealed class TypeMembersQueryService(
    TypeMembersQueryOperation operation) : ITypeMembersQueryService
{
    /// <inheritdoc />
    public async ValueTask<GetTypeMembersResult> GetTypeMembersAsync(
        GetTypeMembersRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await operation.ExecuteAsync(request, cancellationToken);

        return QueryResultMapper.Map(
            result,
            static success => success,
            static error => new GetTypeMembersResult(
                Type: error.Symbol,
                Members: Array.Empty<TypeMemberDescriptor>(),
                FailureReason: error.FailureReason,
                Guidance: error.Guidance),
            static () => new GetTypeMembersResult(
                Type: null,
                Members: Array.Empty<TypeMemberDescriptor>(),
                FailureReason: "The operation was canceled.",
                Guidance: "Retry the request when the operation can run to completion."));
    }
}
