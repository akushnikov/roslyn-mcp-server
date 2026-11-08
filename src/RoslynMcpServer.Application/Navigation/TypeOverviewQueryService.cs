using RoslynMcpServer.Abstractions.Navigation.Models;
using RoslynMcpServer.Abstractions.Navigation.Requests;
using RoslynMcpServer.Abstractions.Navigation.Results;
using RoslynMcpServer.Abstractions.Navigation.Services;
using RoslynMcpServer.Application.Navigation.Operations;
using RoslynMcpServer.Application.QueryPipeline;

namespace RoslynMcpServer.Application.Navigation;

/// <summary>Projects type overview outcomes while preserving successful payloads and contextual failures.</summary>
public sealed class TypeOverviewQueryService(TypeOverviewQueryOperation operation) : ITypeOverviewQueryService
{
    /// <inheritdoc />
    public async ValueTask<GetTypeOverviewResult> GetTypeOverviewAsync(
        GetTypeOverviewRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await operation.ExecuteAsync(request, cancellationToken);

        return QueryResultMapper.Map(
            result,
            static success => success,
            static error => new GetTypeOverviewResult(
                Type: error.Symbol,
                BaseType: null,
                Interfaces: Array.Empty<SymbolDescriptor>(),
                Members: Array.Empty<TypeMemberDescriptor>(),
                FailureReason: error.FailureReason,
                Guidance: error.Guidance),
            static () => new GetTypeOverviewResult(
                Type: null,
                BaseType: null,
                Interfaces: Array.Empty<SymbolDescriptor>(),
                Members: Array.Empty<TypeMemberDescriptor>(),
                FailureReason: "The operation was canceled.",
                Guidance: "Retry the request when the operation can run to completion."));
    }
}
